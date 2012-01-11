﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;

namespace SendGridMail
{
    public class Header : IHeader
    {
        private readonly FilterNode _settings;

        public Header()
        {
            _settings = new FilterNode();
        }

        public void AddSubVal(string tag, IEnumerable<string> substitutions)
        {
            var keys = new List<String> {"data", "sub", tag};
            _settings.AddArray(keys, substitutions);
        }

        public void AddUniqueIdentifier(IDictionary<string, string> identifiers)
        {
            foreach (var key in identifiers.Keys)
            {
                var keys = new List<String> {"data", "unique_args", key};
                var value = identifiers[key];
                _settings.AddSetting(keys, value);
            }
        }

        public void SetCategory(string category)
        {
            var keys = new List<String> {"data", "category"};
            _settings.AddSetting(keys, category);
        }

        public void Enable(string filter)
        {
            AddFilterSetting(filter, new List<string>(){ "enable" }, "1");
        }

        public void Disable(string filter)
        {
            AddFilterSetting(filter, new List<string>(){"enable"}, "0");
        }

        public void AddFilterSetting(string filter, IEnumerable<string> settings, string value)
        {
            var keys = new List<string>() {"filters", "data", filter, "settings"}.Concat(settings).ToList();
            _settings.AddSetting(keys, value);
        }

        public void AddHeader(MailMessage mime)
        {
            mime.Headers.Add("x-smtpapi", AsJson());
        }

        public String AsJson()
        {
            if(_settings.IsEmpty()) return "";
            return _settings.ToJson();
        }

        internal class FilterNode
        {
            private readonly Dictionary<String, FilterNode> _branches;
            private IEnumerable<String> _array; 
            private String _leaf;

            public FilterNode()
            {
                _branches = new Dictionary<string, FilterNode>();
            }

            public void AddArray(List<String> keys, IEnumerable<String> value)
            {
                if (keys.Count == 0)
                {
                    _array = value;
                }
                else
                {
                    if (_leaf != null || _array != null)
                        throw new ArgumentException("Attempt to overwrite setting");

                    var key = keys.First();
                    if (!_branches.ContainsKey(key))
                        _branches[key] = new FilterNode();

                    var remainingKeys = keys.Skip(1).ToList();
                    _branches[key].AddArray(remainingKeys, value);
                }
            }

            public void AddSetting(List<String> keys, String value)
            {
                if (keys.Count == 0)
                {
                    _leaf = value;
                }
                else
                {
                    if(_leaf != null || _array != null) 
                        throw new ArgumentException("Attempt to overwrite setting");
                    
                    var key = keys.First();
                    if (!_branches.ContainsKey(key))
                        _branches[key] = new FilterNode();
                    
                    var remainingKeys = keys.Skip(1).ToList();
                    _branches[key].AddSetting(remainingKeys, value);
                }
            }

            public String GetSetting(params String[] keys)
            {
                return GetSetting(keys.ToList());
            }

            public String GetSetting(List<String> keys)
            {
                if (keys.Count == 0)
                    return _leaf;
                var key = keys.First();
                if(!_branches.ContainsKey(key))
                    throw new ArgumentException("Bad key path!");
                var remainingKeys = keys.Skip(1).ToList();
                return _branches[key].GetSetting(remainingKeys);
            }

            public String GetLeaf()
            {
                return _leaf;
            }

            public String ToJson()
            {
                if (_branches.Count > 0)
                    return "{" + String.Join(",", _branches.Keys.Select(k => JsonUtils.Serialize(k)  + " : " + _branches[k].ToJson())) + "}";
                if (_leaf != null)
                    return JsonUtils.Serialize(_leaf);
                if (_array != null)
                    return "[" + String.Join(",", _array.Select(i => JsonUtils.Serialize(i))) + "]";
                return "{}";
            }

            public bool IsEmpty()
            {
                if (_leaf != null) return false;
                return _branches == null || _branches.Keys.Count == 0;
            }
        }
    }
}
