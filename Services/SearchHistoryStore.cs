using System.Text.Json;
using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class SearchHistoryStore
    {
        private static string Key(string userId) => $"search_history_{userId}";

        public static List<string> Get(string userId)
        {
            var json = Preferences.Get(Key(userId), "[]");
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void Save(string userId, List<string> items)
        {
            var json = JsonSerializer.Serialize(items);
            Preferences.Set(Key(userId), json);
        }

        /// <summary>
        /// 只有“有效搜索并点击阅读”才调用这个方法。
        /// 规则：最多7条；新增插入到最前；超过则删除最旧（末尾）。
        /// </summary>
        public static void AddOnRead(string userId, string keyword)
        {
            keyword = (keyword ?? "").Trim();
            if (keyword.Length == 0) return;

            var list = Get(userId);

            // 去重（不允许叠加效果）
            list.RemoveAll(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase));

            // 最新放前面
            list.Insert(0, keyword);

            // 最多7条
            if (list.Count > 7)
                list = list.Take(7).ToList();

            Save(userId, list);
        }

        public static void Delete(string userId, string keyword)
        {
            var list = Get(userId);
            list.RemoveAll(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase));
            Save(userId, list);
        }

        public static void Clear(string userId)
        {
            Save(userId, new List<string>());
        }
    }
}