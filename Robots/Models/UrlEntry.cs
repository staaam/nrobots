﻿using System;
using System.Text.RegularExpressions;

namespace Robots.Model
{
    public abstract class UrlEntry : Entry
    {
        protected UrlEntry(EntryType type)
            : base(type)
        { }

        public string Url { get; set; }

        private Regex _regex = null;
        public Regex Regex
        {
            get
            {
                return _regex ?? (_regex = CreateRegex());
            }
        }

        protected Regex CreateRegex()
        {
            return new Regex(
                "^" +
                Regex.Escape(Url)
                    .Replace(@"/\*\$", "/[^/]*$")
                    .Replace(@"/\*/", "/[^/]*/")
                    .Replace(@"\*", ".*")
                    .Replace(@"\$", "$"),
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }


    }
}