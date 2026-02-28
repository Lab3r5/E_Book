using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Graphics.Drawables;
using Android.Animation;
using Android.Views.Animations;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using System.Collections.Generic;

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
        private NavigationBarView? _bar;
        private readonly List<Android.Views.View> _itemViews = new();

        private Android.Views.View? _pill;
        private int _lastIndex = -1;
        private bool _started = false;

        // 胶囊松紧（中间的效果保持不变）
        private const int PillPadH = 18;
        private const int PillPadV = 8;

        // 左右边缘额外扩展：用来“挤掉”你红框那块白色
        // 不够就调大：18 / 22；太多就调小：10 / 8
        private const int EdgeExtra = 14;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var decor = Window.DecorView;
            if (decor == null) return;

            var vto = decor.ViewTreeObserver;
            if (vto == null) return;

            vto.GlobalLayout += OnGlobalLayout;
        }

        private void OnGlobalLayout(object? sender, System.EventArgs e)
        {
            var decor = Window.DecorView;
            if (decor == null) return;

            // 只跑一次
            var vto = decor.ViewTreeObserver;
            if (vto != null) vto.GlobalLayout -= OnGlobalLayout;

            // 找到最底部的 NavigationBarView / BottomNavigationView
            var bars = new List<Android.Views.View>();
            CollectBars(decor, bars);
            if (bars.Count == 0) return;

            Android.Views.View? best = null;
            float bestY = -1;
            foreach (var v in bars)
            {
                float y = v.GetY();
                if (y > bestY) { bestY = y; best = v; }
            }

            if (best is NavigationBarView nbv) _bar = nbv;
            else if (best is BottomNavigationView bnv) _bar = (NavigationBarView)bnv;

            if (_bar == null) return;

            ApplyBaseColors(_bar);

            // ✅ 不依赖 MenuView，直接找每个 Tab 的 ItemView
            _itemViews.Clear();
            CollectItemViews(_bar, _itemViews);

            // 第一次可能还没布局好，延迟再抓一次
            if (_itemViews.Count == 0)
            {
                _bar.PostDelayed(() =>
                {
                    if (_bar == null) return;
                    _itemViews.Clear();
                    CollectItemViews(_bar, _itemViews);
                    if (_itemViews.Count > 0)
                    {
                        EnsurePillOnBar(_bar);
                        StartPump();
                    }
                }, 120);
                return;
            }

            EnsurePillOnBar(_bar);
            StartPump();
        }

        private void StartPump()
        {
            if (_bar == null || _started) return;
            _started = true;
            PumpOnce();
        }

        private void PumpOnce()
        {
            if (_bar == null) return;

            // 有些时候 TabBar 会重建，itemViews 可能会丢，需要补抓
            if (_itemViews.Count == 0)
                CollectItemViews(_bar, _itemViews);

            int idx = GetCheckedIndex(_bar);
            if (idx != -1 && idx < _itemViews.Count)
            {
                if (_pill == null)
                    EnsurePillOnBar(_bar);

                if (_pill != null)
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
                        // index 没变但 pill 还没尺寸/没对齐，补一次
                        if (_pill.Width == 0 || _pill.Height == 0)
                            SnapTo(idx);
                    }
                }
            }

            // 轮询刷新，不拦截点击，不影响 Shell 切换
            _bar.PostDelayed(() => PumpOnce(), 150);
        }

        private void ApplyBaseColors(NavigationBarView bar)
        {
            // 🔥 判断当前是否是 Dark 模式
            bool isNight =
                (Resources?.Configuration?.UiMode & Android.Content.Res.UiMode.NightMask)
                == Android.Content.Res.UiMode.NightYes;

            // ✅ 根据模式设置底部栏背景
            var bgColor = isNight
                ? Android.Graphics.Color.Rgb(0x19, 0x13, 0x30)  // Gray950
                : Android.Graphics.Color.White;

            bar.SetBackgroundColor(bgColor);
            bar.Elevation = 0;
            bar.SetClipChildren(false);
            bar.SetClipToPadding(false);

            // 选中 / 未选中颜色
            var states = new int[][]
            {
        new int[] { Android.Resource.Attribute.StateChecked },
        new int[] { -Android.Resource.Attribute.StateChecked }
            };

            var selectedColor = Android.Graphics.Color.White;

            var unselectedColor = isNight
                ? Android.Graphics.Color.Rgb(0x8A, 0x86, 0x9E)  // Gray500
                : Android.Graphics.Color.Rgb(0xB7, 0xB3, 0xC6); // #B7B3C6

            var colors = new int[]
            {
        selectedColor,
        unselectedColor
            };

            var csl = new Android.Content.Res.ColorStateList(states, colors);
            bar.ItemIconTintList = csl;
            bar.ItemTextColor = csl;
        }

        private void EnsurePillOnBar(NavigationBarView bar)
        {
            if (_pill != null) return;

            var pill = new Android.Views.View(this);

            var bg = new GradientDrawable();
            bg.SetColor(Android.Graphics.Color.Rgb(0x7A, 0x63, 0xFF)); // #7A63FF
            bg.SetCornerRadius(999f);
            pill.Background = bg;

            // 不挡点击
            pill.Clickable = false;
            pill.Focusable = false;

            // 挂到 bar 的最底层（index 0）
            if (bar is ViewGroup vg)
                vg.AddView(pill, 0);

            _pill = pill;
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

        // 通过 class name 找 item view（兼容 Material2/3）
        private void CollectItemViews(Android.Views.View root, List<Android.Views.View> list)
        {
            if (root == null) return;

            string name = root.Class?.SimpleName ?? "";

            // 常见：NavigationBarItemView / BottomNavigationItemView
            if (name.Contains("ItemView") && (name.Contains("NavigationBar") || name.Contains("BottomNavigation")))
            {
                list.Add(root);
                return;
            }

            if (root is ViewGroup vg)
            {
                for (int i = 0; i < vg.ChildCount; i++)
                    CollectItemViews(vg.GetChildAt(i), list);
            }
        }

        private void SnapTo(int index)
        {
            if (_bar == null || _pill == null) return;
            if (index < 0 || index >= _itemViews.Count) return;

            var item = _itemViews[index];

            // item 还没布局好就延迟再来
            if (item.Width == 0 || item.Height == 0)
            {
                _bar.PostDelayed(() => SnapTo(index), 60);
                return;
            }

            var t = CalcTargetOnBar(item, index, _itemViews.Count);

            var lp = _pill.LayoutParameters;
            if (lp == null) lp = new ViewGroup.LayoutParams(t.w, t.h);
            lp.Width = t.w;
            lp.Height = t.h;
            _pill.LayoutParameters = lp;

            _pill.TranslationX = t.x;
            _pill.TranslationY = t.y;
        }

        private void AnimateTo(int index)
        {
            if (_bar == null || _pill == null) return;
            if (index < 0 || index >= _itemViews.Count) return;

            var item = _itemViews[index];

            // item 还没布局好就延迟再来
            if (item.Width == 0 || item.Height == 0)
            {
                _bar.PostDelayed(() => AnimateTo(index), 60);
                return;
            }

            var t = CalcTargetOnBar(item, index, _itemViews.Count);

            // —— Pro 动效参数（你可以微调）
            const long MoveDur = 320;      // 移动时长（更丝滑）
            const long SizeDur = 260;      // 宽高回弹时长
            const float Overshoot = 0.95f; // 弹性强度（0.8-1.2 推荐）
            const float Punch = 1.12f;     // 宽度“先放大”倍率（1.08-1.18 推荐）
            const float FadeTo = 0.92f;    // 移动中轻微淡出

            // 目标尺寸
            int targetW = t.w;
            int targetH = t.h;

            // 当前尺寸（首次可能为 0）
            int curW = _pill.Width <= 0 ? targetW : _pill.Width;
            int curH = _pill.Height <= 0 ? targetH : _pill.Height;

            // 1) 位移动画（带弹性）
            var moveX = ObjectAnimator.OfFloat(_pill, "translationX", _pill.TranslationX, t.x);
            var moveY = ObjectAnimator.OfFloat(_pill, "translationY", _pill.TranslationY, t.y);
            moveX.SetDuration(MoveDur);
            moveY.SetDuration(MoveDur);
            moveX.SetInterpolator(new OvershootInterpolator(Overshoot));
            moveY.SetInterpolator(new OvershootInterpolator(Overshoot));

            // 2) 宽度 “Punch” 动画：先变宽一点，再回到目标宽
            int punchW = (int)(targetW * Punch);

            var widthAnim = ValueAnimator.OfInt(curW, punchW, targetW);
            widthAnim.SetDuration(SizeDur);
            widthAnim.SetInterpolator(new OvershootInterpolator(Overshoot));
            widthAnim.Update += (s, e) =>
            {
                if (_pill == null) return;
                var lp = _pill.LayoutParameters;
                if (lp == null) return;
                lp.Width = (int)widthAnim.AnimatedValue;
                _pill.LayoutParameters = lp;
            };

            // 3) 高度微调：稍微“呼吸”一下（可选，但看起来更高级）
            int punchH = (int)(targetH * 1.03f);
            var heightAnim = ValueAnimator.OfInt(curH, punchH, targetH);
            heightAnim.SetDuration(SizeDur);
            heightAnim.SetInterpolator(new OvershootInterpolator(Overshoot));
            heightAnim.Update += (s, e) =>
            {
                if (_pill == null) return;
                var lp = _pill.LayoutParameters;
                if (lp == null) return;
                lp.Height = (int)heightAnim.AnimatedValue;
                _pill.LayoutParameters = lp;
            };

            // 4) 透明度：移动时轻微淡出 -> 回到 1（很丝滑）
            var fade = ObjectAnimator.OfFloat(_pill, "alpha", 1f, FadeTo, 1f);
            fade.SetDuration(MoveDur);

            // 5) 组合一起播放
            var set = new AnimatorSet();
            set.PlayTogether(moveX, moveY, widthAnim, heightAnim, fade);
            set.Start();
        }

        // ✅ 计算 item 相对 bar 的目标位置，并对左右端进行“贴边挤满”
        private (float x, float y, int w, int h) CalcTargetOnBar(Android.Views.View item, int index, int total)
        {
            if (_bar == null) return (0, 0, 0, 0);

            int[] barLoc = new int[2];
            int[] itemLoc = new int[2];
            _bar.GetLocationOnScreen(barLoc);
            item.GetLocationOnScreen(itemLoc);

            float relX = itemLoc[0] - barLoc[0];
            float relY = itemLoc[1] - barLoc[1];

            // 中间默认不变
            int w = item.Width + PillPadH * 2;
            int h = item.Height - PillPadV * 2;
            if (h < 1) h = item.Height;

            float x = relX - PillPadH;
            float y = relY + PillPadV;

            // 左右端挤满：把边缘白色吃掉
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

        private void CollectBars(Android.Views.View root, List<Android.Views.View> list)
        {
            if (root is NavigationBarView || root is BottomNavigationView)
                list.Add(root);

            if (root is ViewGroup vg)
            {
                for (int i = 0; i < vg.ChildCount; i++)
                    CollectBars(vg.GetChildAt(i), list);
            }
        }
    }
}