using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PatchGUIlite.Core
{
    /// <summary>
    /// 一整套规则集合，对应一个游戏或一个规则文件。
    /// </summary>
    public sealed class PatchRuleSet
    {
        public string GameId { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public List<PatchRule> Rules { get; set; } = new();
    }

    /// <summary>
    /// 单条规则：你可以以后加 action、参数等。
    /// </summary>
    public sealed class PatchRule
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 源路径（相对游戏根目录），比如 "data/original.dat"
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 目标路径（相对游戏根目录），比如 "data/translated.dat"
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// 将来扩展动作类型，比如 copy / xdelta / replace 等。
        /// 现在先占位。
        /// </summary>
        public string Action { get; set; } = "copy";

        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// 规则 JSON 的序列化/反序列化。
    /// </summary>
    public static class RuleSetSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public static PatchRuleSet FromJsonString(string json)
        {
            var obj = JsonSerializer.Deserialize<PatchRuleSet>(json, Options);
            if (obj == null)
                throw new InvalidDataException("无法解析规则 JSON。");

            return obj;
        }

        public static PatchRuleSet FromJsonFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("规则 JSON 文件不存在。", path);

            string json = File.ReadAllText(path);
            return FromJsonString(json);
        }

        public static string ToJsonString(PatchRuleSet ruleSet)
        {
            if (ruleSet == null) throw new ArgumentNullException(nameof(ruleSet));
            return JsonSerializer.Serialize(ruleSet, Options);
        }

        public static void ToJsonFile(PatchRuleSet ruleSet, string path)
        {
            string json = ToJsonString(ruleSet);
            File.WriteAllText(path, json);
        }
    }
}

