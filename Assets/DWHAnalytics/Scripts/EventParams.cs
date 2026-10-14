using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DWH.Analytics
{
    /// <summary>
    /// 이벤트 파라미터 묶음. GA4는 문자열과 숫자만 받으므로 bool은 0/1로 바꾼다.
    /// 사용: new EventParams().Add("result", "clear").Add("gold_earned", 120)
    /// </summary>
    public class EventParams
    {
        readonly List<KeyValuePair<string, object>> _items = new List<KeyValuePair<string, object>>();

        public int Count => _items.Count;

        public EventParams Add(string key, string value) { return Put(key, value ?? ""); }
        public EventParams Add(string key, int value) { return Put(key, value); }
        public EventParams Add(string key, long value) { return Put(key, value); }
        public EventParams Add(string key, float value) { return Put(key, value); }
        public EventParams Add(string key, double value) { return Put(key, value); }
        public EventParams Add(string key, bool value) { return Put(key, value ? 1 : 0); }

        EventParams Put(string key, object value)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Key == key)
                {
                    _items[i] = new KeyValuePair<string, object>(key, value);
                    return this;
                }
            }
            _items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        /// <summary>다른 묶음의 값을 모두 넣는다 (같은 키는 덮어쓴다)</summary>
        public EventParams AddAll(EventParams other)
        {
            if (other != null) foreach (var kv in other._items) Put(kv.Key, kv.Value);
            return this;
        }

        public bool Has(string key)
        {
            foreach (var kv in _items) if (kv.Key == key) return true;
            return false;
        }

        public string ToJson()
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < _items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendString(sb, _items[i].Key);
                sb.Append(':');
                AppendValue(sb, _items[i].Value);
            }
            return sb.Append('}').ToString();
        }

        static void AppendValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case string s: AppendString(sb, s); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "0" : f.ToString("0.###", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "0" : d.ToString("0.###", CultureInfo.InvariantCulture)); break;
                default: AppendString(sb, v?.ToString() ?? ""); break;
            }
        }

        static void AppendString(StringBuilder sb, string s)
        {
            // GA4 파라미터 값은 100자까지만 저장된다.
            if (s.Length > 100) s = s.Substring(0, 100);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
