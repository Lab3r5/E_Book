using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Graphics.Drawables;
using Android.Animation;
using Android.Views.Animations;

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

        private bool _pumpStarted = false;
        private bool _ensureLoopRunning = false;
        private int _ensureTries = 0;

        // 胶囊松紧（中间效果保持不变）
        private const int PillPadH = 18;
        private const int PillPadV = 8;

        // 左右边缘额外扩展：用来“挤掉”边缘白色
        private const int EdgeExtra = 14;

        private const string PillTag = "__EBOOK_PILL__";

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

        public void RebindBottomTabBar()
        {
            ResetAllState();
            StartEnsureLoop();
        }

        // =========================
        // Disposed guards
        // =========================
        private static bool IsDisposed(Java.Lang.Object? obj)
        {
            if (obj == null) return true;
            try { return obj.Handle == IntPtr.Zero; }
            catch { return true; }
        }

        private bool IsBarAlive()
        {
            if (_bar == null) return false;
            if (IsDisposed(_bar)) return false;

            try { return _bar.IsAttachedToWindow; }
            catch { return false; }
        }

        // =========================
        // Ensure loop
        // =========================
        private void StartEnsureLoop()
        {
            if (_ensureLoopRunning) return;

            _ensureLoopRunning = true;
            _ensureTries = 0;

            Window.DecorView?.Post(EnsureOnce);
        }

        private void EnsureOnce()
        {
            _ensureTries++;

            bool ok = false;
            try { ok = TryFindBarAndAttach(); }
            catch { ok = false; }

            if (ok)
            {
                _ensureLoopRunning = false;
                return;
            }

            if (_ensureTries < 30)
                Window.DecorView?.PostDelayed(EnsureOnce, 150);
            else
                _ensureLoopRunning = false;
        }

        private bool TryFindBarAndAttach()
        {
            var decor = Window.DecorView;
            if (decor == null) return false;

            // bar 被重建/释放时，清状态
            if (!IsBarAlive())
            {
                RemovePillIfExists();
                _bar = null;
                _itemViews.Clear();
                _lastIndex = -1;
                _pumpStarted = false;
            }

            var bars = new List<AView>();
            CollectBars(decor, bars);
            if (bars.Count == 0) return false;

            NavigationBarView? best = null;
            float bestY = -1;

            foreach (var v in bars)
            {
                var nb = v as NavigationBarView;     // ✅ BottomNavigationView 也会被当成 NavigationBarView
                if (nb == null) continue;
                if (IsDisposed(nb)) continue;

                int menuSize = 0;
                try { menuSize = nb.Menu?.Size() ?? 0; } catch { menuSize = 0; }
                if (menuSize <= 0) continue;

                float y = -1;
                try { y = v.GetY(); } catch { y = -1; }

                if (y > bestY)
                {
                    bestY = y;
                    best = nb;
                }
            }

            if (best == null) return false;

            // bar 变化：重建
            if (_bar == null || !ReferenceEquals(_bar, best))
            {
                RemovePillIfExists();

                _bar = best;
                _itemViews.Clear();
                _lastIndex = -1;
                _pumpStarted = false;

                // ✅ 每次抓到新 bar 都强制应用样式（禁用系统胶囊）
                ApplyBaseColors(_bar);

                // ✅ 清理历史残留 pill
                RemoveOtherPillsFromBar(_bar, keep: null);
            }
            else
            {
                // ✅ 即使 bar 没变，也要再 apply 一次，防止切换后 Material 重置样式
                ApplyBaseColors(_bar);
            }

            if (!IsBarAlive()) return false;

            // ✅ 优先用 MenuView 子项抓 item（顺序最稳定）
            _itemViews.Clear();
            CollectMenuChildrenAsItems(_bar, _itemViews);

            int expected = 0;
            try { expected = _bar.Menu?.Size() ?? 0; } catch { expected = 0; }

            // fallback
            if (_itemViews.Count < expected)
            {
                _itemViews.Clear();
                CollectItemViews(_bar, _itemViews);
            }

            if (expected <= 0) return false;
            if (_itemViews.Count < expected) return false;

            EnsurePillOnBar(_bar);
            EnsureCheckedFallback(_bar);

            int idx = GetCheckedIndex(_bar);
            if (idx < 0 || idx >= _itemViews.Count) idx = 0;

            _lastIndex = idx;
            SnapTo(idx);

            StartPump();
            return true;
        }

        private void ResetAllState()
        {
            RemovePillIfExists();

            if (_bar != null && !IsDisposed(_bar))
                RemoveOtherPillsFromBar(_bar, keep: null);

            _bar = null;
            _itemViews.Clear();
            _lastIndex = -1;
            _pumpStarted = false;
            _ensureLoopRunning = false;
            _ensureTries = 0;
        }

        // =========================
        // Pump loop (不会影响 Shell 点击)
        // =========================
        private void StartPump()
        {
            if (_bar == null || _pumpStarted) return;
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
                if (_bar == null) { _pumpStarted = false; return; }

                // ✅ 关键：每轮都强制禁用系统 ActiveIndicator（切换后它会自己回来）
                ApplyBaseColors(_bar);

                if (_itemViews.Count == 0)
                {
                    CollectMenuChildrenAsItems(_bar, _itemViews);
                    int expected = 0; try { expected = _bar.Menu?.Size() ?? 0; } catch { expected = 0; }

                    if (_itemViews.Count < expected)
                    {
                        _itemViews.Clear();
                        CollectItemViews(_bar, _itemViews);
                    }
                }

                EnsureCheckedFallback(_bar);
                EnsurePillOnBar(_bar);

                int idx = GetCheckedIndex(_bar);
                if (idx >= 0 && idx < _itemViews.Count && _pill != null)
                {
                    if (_lastIndex == -1)
                    {
                        _lastIndex = idx;
                        SnapTo(idx);
                    }
                    else if (idx != _lastIndex)
                    {
                        _lastIndex = idx;
                        AnimateTo(idx);
                    }
                    else
                    {
                        if (_pill.Width == 0 || _pill.Height == 0)
                            SnapTo(idx);
                    }
                }

                if (IsBarAlive())
                    _bar.PostDelayed(PumpOnce, 120);
            }
            catch
            {
                _pumpStarted = false;
                ResetAllState();
                StartEnsureLoop();
            }
        }

        // =========================
        // Styling (✅ 禁用系统胶囊就在这里)
        // =========================
        private void ApplyBaseColors(NavigationBarView bar)
        {
            bool isNight =
                (Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask)
                == Android.Content.Res.UiMode.NightYes;

            var bgColor = isNight
                ? Android.Graphics.Color.Rgb(0x19, 0x13, 0x30)
                : Android.Graphics.Color.White;

            bar.Background = new ColorDrawable(bgColor);
            bar.Elevation = 0;
            bar.SetClipChildren(false);
            bar.SetClipToPadding(false);

            // ✅ 关键：关闭 Material3 的 Active Indicator（系统自带胶囊）
            try { bar.ItemActiveIndicatorEnabled = false; } catch { }
            try { bar.ItemActiveIndicatorColor = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent); } catch { }

            // 可选：关闭涟漪，避免视觉盖住 pill
            try { bar.ItemRippleColor = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent); } catch { }

            // icon/text tint
            var states = new int[][]
            {
                new int[] { Android.Resource.Attribute.StateChecked },
                new int[] { -Android.Resource.Attribute.StateChecked }
            };

            var selectedColor = Android.Graphics.Color.White;

            var unselectedColor = isNight
                ? Android.Graphics.Color.Rgb(0x8A, 0x86, 0x9E)
                : Android.Graphics.Color.Rgb(0xB7, 0xB3, 0xC6);

            var colors = new int[] { selectedColor, unselectedColor };
            var csl = new Android.Content.Res.ColorStateList(states, colors);

            bar.ItemIconTintList = csl;
            bar.ItemTextColor = csl;
        }

        // =========================
        // Pill helpers
        // =========================
        private void RemovePillIfExists()
        {
            try
            {
                if (_pill != null && _pill.Parent is AViewGroup vg)
                    vg.RemoveView(_pill);
            }
            catch { }
            finally { _pill = null; }
        }

        private void RemoveOtherPillsFromBar(NavigationBarView bar, AView? keep)
        {
            if (bar is not AViewGroup vg) return;

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
            bg.SetColor(Android.Graphics.Color.Rgb(0x7A, 0x63, 0xFF));
            bg.SetCornerRadius(999f);
            pill.Background = bg;

            pill.Clickable = false;
            pill.Focusable = false;
            pill.Tag = PillTag;

            if (bar is AViewGroup vg)
                vg.AddView(pill, 0);

            _pill = pill;
        }

        // =========================
        // Checked fallback
        // =========================
        private void EnsureCheckedFallback(NavigationBarView bar)
        {
            try
            {
                var menu = bar.Menu;
                if (menu == null) return;

                for (int i = 0; i < menu.Size(); i++)
                    if (menu.GetItem(i).IsChecked) return;

                if (menu.Size() > 0)
                    menu.GetItem(0).SetChecked(true);
            }
            catch { }
        }

        private int GetCheckedIndex(NavigationBarView bar)
        {
            try
            {
                var menu = bar.Menu;
                if (menu == null) return -1;

                for (int i = 0; i < menu.Size(); i++)
                    if (menu.GetItem(i).IsChecked) return i;
            }
            catch { }

            return -1;
        }

        // =========================
        // Positioning + animation
        // =========================
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

            var lp = _pill.LayoutParameters;
            if (lp == null) lp = new AViewGroup.LayoutParams(t.w, t.h);
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

            var t = CalcTargetOnBar(item, index, _itemViews.Count);

            const long MoveDur = 280;
            const long SizeDur = 240;
            const float Overshoot = 0.9f;

            var moveX = ObjectAnimator.OfFloat(_pill, "translationX", _pill.TranslationX, t.x);
            var moveY = ObjectAnimator.OfFloat(_pill, "translationY", _pill.TranslationY, t.y);
            moveX.SetDuration(MoveDur);
            moveY.SetDuration(MoveDur);
            moveX.SetInterpolator(new OvershootInterpolator(Overshoot));
            moveY.SetInterpolator(new OvershootInterpolator(Overshoot));

            int targetW = t.w;
            int targetH = t.h;

            int curW = _pill.Width <= 0 ? targetW : _pill.Width;
            int curH = _pill.Height <= 0 ? targetH : _pill.Height;

            var widthAnim = ValueAnimator.OfInt(curW, targetW);
            widthAnim.SetDuration(SizeDur);
            widthAnim.SetInterpolator(new OvershootInterpolator(Overshoot));
            widthAnim.Update += (s, e) =>
            {
                var lp = _pill.LayoutParameters;
                if (lp == null) return;
                lp.Width = (int)widthAnim.AnimatedValue;
                _pill.LayoutParameters = lp;
            };

            var heightAnim = ValueAnimator.OfInt(curH, targetH);
            heightAnim.SetDuration(SizeDur);
            heightAnim.SetInterpolator(new OvershootInterpolator(Overshoot));
            heightAnim.Update += (s, e) =>
            {
                var lp = _pill.LayoutParameters;
                if (lp == null) return;
                lp.Height = (int)heightAnim.AnimatedValue;
                _pill.LayoutParameters = lp;
            };

            var set = new AnimatorSet();
            set.PlayTogether(moveX, moveY, widthAnim, heightAnim);
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

        // =========================
        // Find bar & items
        // =========================
        private void CollectBars(AView root, List<AView> list)
        {
            if (root is NavigationBarView) list.Add(root);
            if (root is BottomNavigationView bnv) list.Add(bnv);

            if (root is AViewGroup vg)
            {
                for (int i = 0; i < vg.ChildCount; i++)
                    CollectBars(vg.GetChildAt(i), list);
            }
        }

        private void CollectItemViews(AView root, List<AView> list)
        {
            if (root == null) return;

            string name = root.Class?.SimpleName ?? "";

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

            string name = root.Class?.SimpleName ?? "";

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