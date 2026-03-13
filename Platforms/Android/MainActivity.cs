using Android.Animation;
using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;

using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;

using System;
using System.Collections.Generic;

using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace E_Book
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize |
                               ConfigChanges.Orientation |
                               ConfigChanges.UiMode |
                               ConfigChanges.ScreenLayout |
                               ConfigChanges.SmallestScreenSize |
                               ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static MainActivity? Instance { get; private set; }

        private NavigationBarView? _bar;
        private readonly List<AView> _itemViews = new();

        private AView? _pill;
        private int _lastIndex = -1;

        private bool _pumpStarted;
        private bool _ensureLoopRunning;
        private int _ensureTries;

        private AnimatorSet? _currentAnimator;

        private const string PillTag = "__EBOOK_PILL__";

        private const int PillPadH = 18;
        private const int PillPadV = 11;
        private const int EdgeExtra = 14;

        private const long MoveDuration = 280L;
        private const long ResizeDuration = 240L;
        private const float AnimationOvershoot = 0.9f;

        private bool? _lastNightMode;

        private readonly Android.Graphics.Color _selectedColor = Android.Graphics.Color.White;
        private readonly Android.Graphics.Color _lightUnselectedColor = Android.Graphics.Color.Rgb(0xB7, 0xB3, 0xC6);
        private readonly Android.Graphics.Color _darkUnselectedColor = Android.Graphics.Color.Rgb(0x8A, 0x86, 0x9E);

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Instance = this;
            StartEnsureLoop();
        }

        protected override void OnResume()
        {
            base.OnResume();
            StartEnsureLoop();
        }

        protected override void OnPause()
        {
            base.OnPause();
            StopCurrentAnimation();
            _pumpStarted = false;
        }

        protected override void OnDestroy()
        {
            StopCurrentAnimation();
            ResetAllState();
            Instance = null;
            base.OnDestroy();
        }

        public void RebindBottomTabBar()
        {
            ResetAllState();
            StartEnsureLoop();
        }

        private static bool IsDisposed(Java.Lang.Object? obj)
        {
            if (obj == null) return true;

            try
            {
                return obj.Handle == IntPtr.Zero;
            }
            catch
            {
                return true;
            }
        }

        private bool IsBarAlive()
        {
            if (_bar == null) return false;
            if (IsDisposed(_bar)) return false;

            try
            {
                return _bar.IsAttachedToWindow;
            }
            catch
            {
                return false;
            }
        }

        private bool IsNightMode()
        {
            return (Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask)
                   == Android.Content.Res.UiMode.NightYes;
        }

        private Android.Graphics.Color GetUnselectedColor()
        {
            return IsNightMode() ? _darkUnselectedColor : _lightUnselectedColor;
        }

        private void StartEnsureLoop()
        {
            if (_ensureLoopRunning)
                return;

            _ensureLoopRunning = true;
            _ensureTries = 0;

            Window.DecorView?.Post(EnsureOnce);
        }

        private void EnsureOnce()
        {
            _ensureTries++;

            bool ok;
            try
            {
                ok = TryFindBarAndAttach();
            }
            catch
            {
                ok = false;
            }

            if (ok)
            {
                _ensureLoopRunning = false;
                return;
            }

            if (_ensureTries < 40)
            {
                Window.DecorView?.PostDelayed(EnsureOnce, 120);
            }
            else
            {
                _ensureLoopRunning = false;
            }
        }

        private bool TryFindBarAndAttach()
        {
            var decor = Window.DecorView;
            if (decor == null) return false;

            if (!IsBarAlive())
            {
                RemovePillIfExists();
                _bar = null;
                _itemViews.Clear();
                _lastIndex = -1;
                _pumpStarted = false;
            }

            var bestBar = FindBestNavigationBar(decor);
            if (bestBar == null) return false;

            PrepareBar(bestBar);

            if (!IsBarAlive() || _bar == null)
                return false;

            if (!TryCollectTabItems(_bar, _itemViews))
                return false;

            EnsurePillOnBar(_bar);
            EnsureCheckedFallback(_bar);

            int idx = GetCheckedIndex(_bar);
            if (idx < 0 || idx >= _itemViews.Count)
                idx = 0;

            _lastIndex = idx;

            ApplyBaseColors(_bar);
            ForceApplyVisualsToAllItems(idx);
            SnapTo(idx);

            StartPump();
            return true;
        }

        private NavigationBarView? FindBestNavigationBar(AView root)
        {
            var bars = new List<NavigationBarView>();
            CollectBars(root, bars);

            if (bars.Count == 0)
                return null;

            NavigationBarView? best = null;
            float bestY = -1f;

            foreach (var bar in bars)
            {
                if (IsDisposed(bar))
                    continue;

                int menuSize;
                try
                {
                    menuSize = bar.Menu?.Size() ?? 0;
                }
                catch
                {
                    menuSize = 0;
                }

                if (menuSize <= 0)
                    continue;

                float y;
                try
                {
                    y = bar.GetY();
                }
                catch
                {
                    y = -1f;
                }

                if (y > bestY)
                {
                    bestY = y;
                    best = bar;
                }
            }

            return best;
        }

        private void PrepareBar(NavigationBarView best)
        {
            bool night = IsNightMode();
            bool themeChanged = _lastNightMode == null || _lastNightMode.Value != night;

            if (_bar == null || !ReferenceEquals(_bar, best))
            {
                RemovePillIfExists();

                _bar = best;
                _itemViews.Clear();
                _lastIndex = -1;
                _pumpStarted = false;

                ApplyBaseColors(_bar);
                RemoveOtherPillsFromBar(_bar, keep: null);
                _lastNightMode = night;
            }
            else if (themeChanged)
            {
                ApplyBaseColors(_bar);
                _lastNightMode = night;
            }
        }

        private bool TryCollectTabItems(NavigationBarView bar, List<AView> items)
        {
            items.Clear();

            int expected;
            try
            {
                expected = bar.Menu?.Size() ?? 0;
            }
            catch
            {
                expected = 0;
            }

            if (expected <= 0)
                return false;

            CollectMenuChildrenAsItems(bar, items);

            if (items.Count >= expected)
                return true;

            items.Clear();
            CollectItemViews(bar, items);

            return items.Count >= expected;
        }

        private void ResetAllState()
        {
            StopCurrentAnimation();
            RemovePillIfExists();

            if (_bar != null && !IsDisposed(_bar))
                RemoveOtherPillsFromBar(_bar, keep: null);

            _bar = null;
            _itemViews.Clear();
            _lastIndex = -1;
            _pumpStarted = false;
            _ensureLoopRunning = false;
            _ensureTries = 0;
            _lastNightMode = null;
        }

        private void StartPump()
        {
            if (_bar == null || _pumpStarted)
                return;

            _pumpStarted = true;
            PumpOnce();
        }

        private void PumpOnce()
        {
            if (!IsBarAlive())
            {
                _pumpStarted = false;
                ResetAllState();
                StartEnsureLoop();
                return;
            }

            try
            {
                if (_bar == null)
                {
                    _pumpStarted = false;
                    return;
                }

                bool currentNight = IsNightMode();
                if (_lastNightMode == null || _lastNightMode.Value != currentNight)
                {
                    ApplyBaseColors(_bar);

                    if (_itemViews.Count == 0)
                        TryCollectTabItems(_bar, _itemViews);

                    if (_lastIndex >= 0)
                        ForceApplyVisualsToAllItems(_lastIndex);

                    _lastNightMode = currentNight;
                }

                if (_itemViews.Count == 0 && !TryCollectTabItems(_bar, _itemViews))
                {
                    if (IsBarAlive())
                        _bar.PostDelayed(PumpOnce, 120);

                    return;
                }

                EnsureCheckedFallback(_bar);
                EnsurePillOnBar(_bar);

                int idx = GetCheckedIndex(_bar);
                if (idx < 0 || idx >= _itemViews.Count)
                    idx = 0;

                // 锁死样式：每一轮都统一所有 tab 的 icon/text
                ForceApplyVisualsToAllItems(idx);

                if (_lastIndex == -1)
                {
                    _lastIndex = idx;
                    SnapTo(idx);
                }
                else if (idx != _lastIndex)
                {
                    _lastIndex = idx;

                    if (_pill.Width == 0 || _pill.Height == 0)
                        SnapTo(idx);
                    else
                        AnimateTo(idx);
                }
                else if (_pill.Width == 0 || _pill.Height == 0)
                {
                    SnapTo(idx);
                }

                // 频率稍微保守一点，既稳又不会太吃性能
                if (IsBarAlive())
                    _bar.PostDelayed(PumpOnce, 180);
            }
            catch
            {
                _pumpStarted = false;
                ResetAllState();
                StartEnsureLoop();
            }
        }

        private void ApplyBaseColors(NavigationBarView bar)
        {
            bool isNight = IsNightMode();

            var bgColor = isNight
                ? Android.Graphics.Color.Rgb(0x19, 0x13, 0x30)
                : Android.Graphics.Color.White;

            bar.Background = new ColorDrawable(bgColor);
            bar.Elevation = 0;
            bar.SetClipChildren(false);
            bar.SetClipToPadding(false);

            try { bar.ItemActiveIndicatorEnabled = false; } catch { }
            try { bar.ItemActiveIndicatorColor = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent); } catch { }
            try { bar.ItemRippleColor = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent); } catch { }

            try
            {
                if (bar is BottomNavigationView bottomBar)
                    bottomBar.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityLabeled;
            }
            catch
            {
            }

            var states = new int[][]
            {
                new int[] { Android.Resource.Attribute.StateChecked },
                new int[] { -Android.Resource.Attribute.StateChecked }
            };

            var colors = new int[]
            {
                _selectedColor.ToArgb(),
                GetUnselectedColor().ToArgb()
            };

            var csl = new Android.Content.Res.ColorStateList(states, colors);

            try
            {
                bar.ItemIconTintList = null;
                bar.ItemTextColor = null;
            }
            catch
            {
            }

            try { bar.ItemIconTintList = csl; } catch { }
            try { bar.ItemTextColor = csl; } catch { }
        }

        private void ForceApplyVisualsToAllItems(int selectedIndex)
        {
            if (_itemViews.Count == 0)
                return;

            for (int i = 0; i < _itemViews.Count; i++)
            {
                bool isSelected = i == selectedIndex;
                var item = _itemViews[i];
                if (item == null) continue;

                ForceApplyVisualsToSingleItem(item, isSelected);
            }
        }

        private void ForceApplyVisualsToSingleItem(AView item, bool isSelected)
        {
            TryAdjustIconInsideItem(item, isSelected);
            TryAdjustAllLabelsInsideItem(item, isSelected);
            TryForceSelectedStateFlags(item, isSelected);
        }

        private void TryAdjustIconInsideItem(AView item, bool isSelected)
        {
            try
            {
                if (item is not AViewGroup vg)
                    return;

                var iconViews = FindAllImageViews(vg);
                if (iconViews.Count == 0)
                    return;

                var selectedColor = _selectedColor;
                var unselectedColor = GetUnselectedColor();

                foreach (var iconView in iconViews)
                {
                    try
                    {
                        iconView.PivotX = iconView.Width / 2f;
                        iconView.PivotY = iconView.Height / 2f;

                        iconView.ScaleX = isSelected ? 1.22f : 1.10f;
                        iconView.ScaleY = isSelected ? 1.22f : 1.10f;
                        iconView.TranslationY = isSelected ? 2f : 1f;
                        iconView.Alpha = 1f;
                        iconView.SetColorFilter(isSelected ? selectedColor : unselectedColor);
                        iconView.Selected = isSelected;
                        iconView.Activated = isSelected;
                        iconView.Enabled = true;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private void TryAdjustAllLabelsInsideItem(AView item, bool isSelected)
        {
            try
            {
                if (item is not AViewGroup vg)
                    return;

                var labels = FindAllTextViews(vg);
                if (labels.Count == 0)
                    return;

                var selectedColor = _selectedColor;
                var unselectedColor = GetUnselectedColor();

                foreach (var label in labels)
                {
                    try
                    {
                        label.SetSingleLine(true);
                        label.SetIncludeFontPadding(false);
                        label.SetTextSize(ComplexUnitType.Sp, 10.5f);
                        label.TranslationY = isSelected ? -1f : -2.5f;
                        label.SetTypeface(label.Typeface, isSelected ? TypefaceStyle.Bold : TypefaceStyle.Normal);
                        label.SetTextColor(isSelected ? selectedColor : unselectedColor);
                        label.Alpha = 1f;
                        label.Selected = isSelected;
                        label.Activated = isSelected;
                        label.Enabled = true;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private void TryForceSelectedStateFlags(AView item, bool isSelected)
        {
            try
            {
                item.Selected = isSelected;
                item.Activated = isSelected;
                item.Enabled = true;
                item.Alpha = 1f;

                if (item is AViewGroup vg)
                {
                    ForceFlagsRecursively(vg, isSelected);
                }
            }
            catch
            {
            }
        }

        private void ForceFlagsRecursively(AViewGroup root, bool isSelected)
        {
            for (int i = 0; i < root.ChildCount; i++)
            {
                var child = root.GetChildAt(i);
                if (child == null) continue;

                try
                {
                    child.Selected = isSelected;
                    child.Activated = isSelected;
                    child.Enabled = true;

                    if (child is TextView tv)
                        tv.Alpha = 1f;

                    if (child is ImageView iv)
                        iv.Alpha = 1f;
                }
                catch
                {
                }

                if (child is AViewGroup childGroup)
                    ForceFlagsRecursively(childGroup, isSelected);
            }
        }

        private List<ImageView> FindAllImageViews(AViewGroup root)
        {
            var result = new List<ImageView>();

            void Collect(AViewGroup group)
            {
                for (int i = 0; i < group.ChildCount; i++)
                {
                    var child = group.GetChildAt(i);

                    if (child is ImageView iv)
                    {
                        result.Add(iv);
                    }
                    else if (child is AViewGroup childGroup)
                    {
                        Collect(childGroup);
                    }
                }
            }

            Collect(root);
            return result;
        }

        private List<TextView> FindAllTextViews(AViewGroup root)
        {
            var result = new List<TextView>();

            void Collect(AViewGroup group)
            {
                for (int i = 0; i < group.ChildCount; i++)
                {
                    var child = group.GetChildAt(i);

                    if (child is TextView tv)
                    {
                        result.Add(tv);
                    }
                    else if (child is AViewGroup childGroup)
                    {
                        Collect(childGroup);
                    }
                }
            }

            Collect(root);
            return result;
        }

        private void StopCurrentAnimation()
        {
            try
            {
                _currentAnimator?.Cancel();
                _currentAnimator?.Dispose();
            }
            catch
            {
            }
            finally
            {
                _currentAnimator = null;
            }
        }

        private void RemovePillIfExists()
        {
            try
            {
                if (_pill != null && _pill.Parent is AViewGroup vg)
                    vg.RemoveView(_pill);
            }
            catch
            {
            }
            finally
            {
                _pill = null;
            }
        }

        private void RemoveOtherPillsFromBar(NavigationBarView bar, AView? keep)
        {
            if (bar is not AViewGroup vg)
                return;

            for (int i = vg.ChildCount - 1; i >= 0; i--)
            {
                var child = vg.GetChildAt(i);
                if (child == null) continue;

                if (child.Tag?.ToString() == PillTag && !ReferenceEquals(child, keep))
                    vg.RemoveView(child);
            }
        }

        private void EnsurePillOnBar(NavigationBarView bar)
        {
            if (_pill != null && _pill.Parent == null)
                _pill = null;

            if (_pill != null && _pill.Parent != bar)
                RemovePillIfExists();

            if (_pill != null)
            {
                RemoveOtherPillsFromBar(bar, keep: _pill);
                return;
            }

            RemoveOtherPillsFromBar(bar, keep: null);

            var pill = new AView(this);

            var bg = new GradientDrawable();
            bg.SetColor(Android.Graphics.Color.Rgb(0x7D, 0x63, 0xFF));
            bg.SetCornerRadius(999f);
            pill.Background = bg;

            pill.Clickable = false;
            pill.Focusable = false;
            pill.Tag = PillTag;

            if (bar is AViewGroup vg)
                vg.AddView(pill, 0);

            _pill = pill;
        }

        private void EnsureCheckedFallback(NavigationBarView bar)
        {
            try
            {
                var menu = bar.Menu;
                if (menu == null) return;

                for (int i = 0; i < menu.Size(); i++)
                {
                    if (menu.GetItem(i).IsChecked)
                        return;
                }

                if (menu.Size() > 0)
                    menu.GetItem(0).SetChecked(true);
            }
            catch
            {
            }
        }

        private int GetCheckedIndex(NavigationBarView bar)
        {
            try
            {
                var menu = bar.Menu;
                if (menu == null) return -1;

                for (int i = 0; i < menu.Size(); i++)
                {
                    if (menu.GetItem(i).IsChecked)
                        return i;
                }
            }
            catch
            {
            }

            return -1;
        }

        private void SnapTo(int index)
        {
            if (_bar == null || _pill == null) return;
            if (!IsBarAlive()) return;
            if (index < 0 || index >= _itemViews.Count) return;

            var item = _itemViews[index];

            if (item.Width == 0 || item.Height == 0)
            {
                _bar.PostDelayed(() => SnapTo(index), 60);
                return;
            }

            var t = CalcTargetOnBar(item, index, _itemViews.Count);

            var lp = _pill.LayoutParameters ?? new AViewGroup.LayoutParams(t.w, t.h);
            lp.Width = t.w;
            lp.Height = t.h;
            _pill.LayoutParameters = lp;

            _pill.TranslationX = t.x;
            _pill.TranslationY = t.y;
        }

        private void AnimateTo(int index)
        {
            if (_bar == null || _pill == null) return;
            if (!IsBarAlive()) return;
            if (index < 0 || index >= _itemViews.Count) return;

            var item = _itemViews[index];

            if (item.Width == 0 || item.Height == 0)
            {
                _bar.PostDelayed(() => AnimateTo(index), 60);
                return;
            }

            StopCurrentAnimation();

            var t = CalcTargetOnBar(item, index, _itemViews.Count);

            var moveX = ObjectAnimator.OfFloat(_pill, "translationX", _pill.TranslationX, t.x);
            var moveY = ObjectAnimator.OfFloat(_pill, "translationY", _pill.TranslationY, t.y);

            moveX.SetDuration(MoveDuration);
            moveY.SetDuration(MoveDuration);
            moveX.SetInterpolator(new OvershootInterpolator(AnimationOvershoot));
            moveY.SetInterpolator(new OvershootInterpolator(AnimationOvershoot));

            int targetW = t.w;
            int targetH = t.h;

            int curW = _pill.Width <= 0 ? targetW : _pill.Width;
            int curH = _pill.Height <= 0 ? targetH : _pill.Height;

            var widthAnim = ValueAnimator.OfInt(curW, targetW);
            widthAnim.SetDuration(ResizeDuration);
            widthAnim.SetInterpolator(new OvershootInterpolator(AnimationOvershoot));
            widthAnim.Update += (s, e) =>
            {
                var lp = _pill.LayoutParameters;
                if (lp == null) return;
                lp.Width = (int)widthAnim.AnimatedValue!;
                _pill.LayoutParameters = lp;
            };

            var heightAnim = ValueAnimator.OfInt(curH, targetH);
            heightAnim.SetDuration(ResizeDuration);
            heightAnim.SetInterpolator(new OvershootInterpolator(AnimationOvershoot));
            heightAnim.Update += (s, e) =>
            {
                var lp = _pill.LayoutParameters;
                if (lp == null) return;
                lp.Height = (int)heightAnim.AnimatedValue!;
                _pill.LayoutParameters = lp;
            };

            var set = new AnimatorSet();
            set.PlayTogether(moveX, moveY, widthAnim, heightAnim);
            _currentAnimator = set;
            set.Start();
        }

        private (float x, float y, int w, int h) CalcTargetOnBar(AView item, int index, int total)
        {
            if (_bar == null) return (0, 0, 0, 0);

            int[] barLoc = new int[2];
            int[] itemLoc = new int[2];

            _bar.GetLocationOnScreen(barLoc);
            item.GetLocationOnScreen(itemLoc);

            float relX = itemLoc[0] - barLoc[0];
            float relY = itemLoc[1] - barLoc[1];

            int w = item.Width + PillPadH * 2;
            int h = item.Height - PillPadV * 2;
            if (h < 1) h = item.Height;

            float x = relX - PillPadH;
            float y = relY + PillPadV;

            if (index == 0)
            {
                float right = x + w;
                x = -EdgeExtra;
                w = (int)(right - x);
            }
            else if (index == total - 1)
            {
                float left = x;
                float barRight = _bar.Width + EdgeExtra;
                w = (int)(barRight - left);
                x = left;
            }

            return (x, y, w, h);
        }

        private void CollectBars(AView root, List<NavigationBarView> list)
        {
            if (root is NavigationBarView navBar)
                list.Add(navBar);

            if (root is AViewGroup vg)
            {
                for (int i = 0; i < vg.ChildCount; i++)
                    CollectBars(vg.GetChildAt(i), list);
            }
        }

        private void CollectItemViews(AView root, List<AView> list)
        {
            if (root == null) return;

            string name = root.Class?.SimpleName ?? string.Empty;

            if (name.Contains("ItemView") &&
                (name.Contains("NavigationBar") || name.Contains("BottomNavigation")))
            {
                list.Add(root);
                return;
            }

            if (root is AViewGroup vg)
            {
                for (int i = 0; i < vg.ChildCount; i++)
                    CollectItemViews(vg.GetChildAt(i), list);
            }
        }

        private void CollectMenuChildrenAsItems(AView root, List<AView> list)
        {
            if (root == null) return;

            string name = root.Class?.SimpleName ?? string.Empty;

            if (name.Contains("MenuView") &&
                (name.Contains("BottomNavigation") || name.Contains("NavigationBar")))
            {
                if (root is AViewGroup vg)
                {
                    for (int i = 0; i < vg.ChildCount; i++)
                        list.Add(vg.GetChildAt(i));
                }
                return;
            }

            if (root is AViewGroup vg2)
            {
                for (int i = 0; i < vg2.ChildCount; i++)
                    CollectMenuChildrenAsItems(vg2.GetChildAt(i), list);
            }
        }
    }
}