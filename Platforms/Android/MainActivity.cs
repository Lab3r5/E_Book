using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Graphics.Drawables;

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
        private NavigationBarView? _bar;
        private readonly List<AView> _items = new();
        private AView? _pill;

        private const int PillPadH = 18;
        private const int PillPadV = 8;
        private const int EdgeExtra = 14;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Window.DecorView?.Post(InitTabBar);
        }

        protected override void OnResume()
        {
            base.OnResume();
            Window.DecorView?.Post(InitTabBar);
        }

        private void InitTabBar()
        {
            var decor = Window.DecorView;
            if (decor == null) return;

            var bars = new List<AView>();
            CollectBars(decor, bars);

            if (bars.Count == 0) return;

            foreach (var v in bars)
            {
                if (v is NavigationBarView nb && nb.Menu?.Size() > 0)
                {
                    _bar = nb;
                    break;
                }
            }

            if (_bar == null) return;

            SetupBarStyle(_bar);
            CollectItemViews(_bar, _items);

            EnsurePill();
            SnapTo(GetCheckedIndex());

            _bar.SetOnItemSelectedListener(new ItemSelectedListener(this));
        }

        // ===============================
        // Styling
        // ===============================
        private void SetupBarStyle(NavigationBarView bar)
        {
            bar.Elevation = 0;
            bar.SetClipChildren(false);
            bar.SetClipToPadding(false);

            var bg = new ColorDrawable(Android.Graphics.Color.White);
            bar.Background = bg;
        }

        // ===============================
        // Pill
        // ===============================
        private void EnsurePill()
        {
            if (_bar == null) return;
            if (_pill != null && _pill.Parent == _bar) return;

            if (_bar is not AViewGroup vg) return;

            var pill = new AView(this);
            var bg = new GradientDrawable();
            bg.SetColor(Android.Graphics.Color.Rgb(0x7A, 0x63, 0xFF));
            bg.SetCornerRadius(999f);
            pill.Background = bg;

            vg.AddView(pill, 0);
            _pill = pill;
        }

        private void SnapTo(int index)
        {
            if (_bar == null || _pill == null) return;
            if (index < 0 || index >= _items.Count) return;

            var item = _items[index];
            if (item.Width == 0) return;

            var t = Calc(item, index);

            var lp = _pill.LayoutParameters ?? new AViewGroup.LayoutParams(t.w, t.h);
            lp.Width = t.w;
            lp.Height = t.h;
            _pill.LayoutParameters = lp;

            _pill.TranslationX = t.x;
            _pill.TranslationY = t.y;
        }

        private (float x, float y, int w, int h) Calc(AView item, int index)
        {
            int[] barLoc = new int[2];
            int[] itemLoc = new int[2];
            _bar!.GetLocationOnScreen(barLoc);
            item.GetLocationOnScreen(itemLoc);

            float relX = itemLoc[0] - barLoc[0];
            float relY = itemLoc[1] - barLoc[1];

            int w = item.Width + PillPadH * 2;
            int h = item.Height - PillPadV * 2;

            float x = relX - PillPadH;
            float y = relY + PillPadV;

            if (index == 0)
            {
                x = -EdgeExtra;
                w += EdgeExtra;
            }
            else if (index == _items.Count - 1)
            {
                w += EdgeExtra;
            }

            return (x, y, w, h);
        }

        private int GetCheckedIndex()
        {
            if (_bar?.Menu == null) return 0;

            for (int i = 0; i < _bar.Menu.Size(); i++)
                if (_bar.Menu.GetItem(i).IsChecked)
                    return i;

            return 0;
        }

        // ===============================
        // Listener
        // ===============================
        private class ItemSelectedListener : Java.Lang.Object, NavigationBarView.IOnItemSelectedListener
        {
            private readonly MainActivity _activity;

            public ItemSelectedListener(MainActivity activity)
            {
                _activity = activity;
            }

            public bool OnNavigationItemSelected(IMenuItem item)
            {
                int index = _activity.GetCheckedIndex();
                _activity.SnapTo(index);
                return false; // 让 Shell 自己处理切换
            }
        }

        // ===============================
        // Utils
        // ===============================
        private void CollectBars(AView root, List<AView> list)
        {
            if (root is NavigationBarView || root is BottomNavigationView)
                list.Add(root);

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

            if (name.Contains("ItemView"))
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
    }
}