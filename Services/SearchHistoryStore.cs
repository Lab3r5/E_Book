using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace E_Book.Services
{
    public static class SearchHistoryStore
    {
        private const int MaxItems = 6;

        private static string Key(string userId)
            => $"search_history_{UserSession.BuildSafeStorageKey(userId)}";

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