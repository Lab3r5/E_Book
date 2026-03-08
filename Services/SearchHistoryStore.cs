using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class SearchHistoryStore
    {
        private const int MaxItems = 10;

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
        /// 搜索时记录历史。
        /// 规则：最多10条；新增插入到最前；重复项先移除再插入。
        /// </summary>
        public static void Add(string userId, string keyword)
        {
            keyword = (keyword ?? "").Trim();
            if (keyword.Length == 0) return;

            var list = Get(userId);

            list.RemoveAll(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, keyword);

            if (list.Count > MaxItems)
                list = list.Take(MaxItems).ToList();

            Save(userId, list);
        }

        /// <summary>
        /// 点击阅读后也可调用，内部仍复用 Add。
        /// </summary>
        public static void AddOnRead(string userId, string keyword)
        {
            Add(userId, keyword);
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