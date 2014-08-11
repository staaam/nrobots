﻿using System;
using System.Collections.Generic;
using System.IO;
using Robots.Model;

namespace Robots
{
    public class Robots : IRobots
    {
        #region Private Fields

        private readonly List<Entry> _entries = new List<Entry>();

        private Uri _baseUri;

        #endregion

        #region Accessors

        public Uri BaseUri
        {
            get { return _baseUri; }
            set
            {
                if (value == _baseUri)
                    return;

                _baseUri = value;
            }
        }
        #endregion

        #region Load methods

        /// <summary>
        /// Load the robots.txt from a web resource
        /// </summary>
        /// <param name="robotsUri">The Uri of the web resource</param>
        public void Load(Uri robotsUri)
        {
            var uriBuilder = new UriBuilder(robotsUri) { Path = "/robots.txt" };
            var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uriBuilder.Uri);
            
            using (var webresponse = req.GetResponse())
            using (var responseStream = webresponse.GetResponseStream())
            {
                string baseUrl = webresponse.ResponseUri.GetLeftPart(UriPartial.Authority);
                Load(responseStream, baseUrl);
            }
        }

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUrl"></param>
        public void Load(Stream stream, string baseUrl)
        {
            Uri baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                throw new ArgumentException("Invalid Url", "baseUrl");
            
            Load(new StreamReader(stream), baseUri);
        }

        /// <summary>
        /// Loads the robots.txt from a stream. 
        /// </summary>
        /// <param name="stream">The input stream. May not be null.</param>
        /// <param name="baseUri"></param>
        public void Load(Stream stream, Uri baseUri)
        {
            Load(new StreamReader(stream), baseUri);
        }

        /// <summary>
        /// Loads the robots.txt from the specified string. 
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt to load. May not be null</param>
        /// <param name="baseUrl"></param>
        public void LoadContent(string fileContent, string baseUrl)
        {
            Uri baseUri;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri))
                throw new ArgumentException("Invalid Url", "baseUrl");

            LoadContent(fileContent, new Uri(baseUrl));
        }

        /// <summary>
        /// Loads the robots.txt from the specified string. 
        /// </summary>
        /// <param name="fileContent">String containing the robots.txt to load. May not be null</param>
        /// <param name="baseUri"></param>
        public void LoadContent(string fileContent, Uri baseUri)
        {
            if (fileContent == null)
                throw new ArgumentNullException("fileContent");
            var sr = new StringReader(fileContent);
            try
            {
                Load(sr, baseUri);
            }
            finally
            {
                sr.Close();
            }
        }

        /// <summary>
        /// Loads the robots.txt from the specified TextReader. 
        /// </summary>
        /// <param name="reader">The TextReader used to feed the robots.txt data into the document. May not be null.</param>
        /// <param name="baseUri"></param>
        public void Load(TextReader reader, Uri baseUri)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            _baseUri = baseUri;

            List<UserAgentEntry> userAgentsGroup = new List<UserAgentEntry>();
            bool addedEntriesToUserAgent = false;
            do
            {
                string line = reader.ReadLine();
                if (line == null)
                    break;

                if (string.IsNullOrEmpty(line))
                    continue;

                Entry entry;
                if (!Entry.TryParse(_baseUri, line, out entry))
                    continue;

                if (entry.Type == EntryType.UserAgent)
                {
                    UserAgentEntry userAgentEntry = (UserAgentEntry)entry;
                    if (addedEntriesToUserAgent)
                    {
                        userAgentsGroup.Clear();
                        addedEntriesToUserAgent = false;
                    }
                    
                    UserAgentEntry foundUserAgentEntry = FindExplicitUserAgentEntry(userAgentEntry.UserAgent);
                    if (foundUserAgentEntry == null)
                    {
                        _entries.Add(userAgentEntry);
                        userAgentsGroup.Add(userAgentEntry);
                    }
                    else
                    {
                        userAgentsGroup.Add(foundUserAgentEntry);
                    }
                }
                else if (entry.Type == EntryType.Comment)
                {
                    _entries.Add(entry);
                }
                else if (entry.Type == EntryType.Sitemap)
                {
                    _entries.Add(entry);
                }
                else if (userAgentsGroup.Count > 0)
                {
                    foreach (UserAgentEntry userAgent in userAgentsGroup)
                    {
                        userAgent.AddEntry(entry);
                    }
                    addedEntriesToUserAgent = true;
                }
                else
                {
                    continue;
                }
            } while (true);

        }

        #endregion

        #region Methods: Allow

        public bool Allowed(string url)
        {
            return Allowed(new Uri(url, UriKind.RelativeOrAbsolute), UserAgents.AllAgents);
        }

        public bool Allowed(string url, string userAgent)
        {
            return Allowed(new Uri(url, UriKind.RelativeOrAbsolute), userAgent);
        }

        public bool Allowed(Uri uri)
        {
            return Allowed(uri, UserAgents.AllAgents);
        }

        public bool Allowed(Uri uri, string userAgent)
        {
            if (_entries.Count == 0)
                return true;

            if (!uri.IsAbsoluteUri)
                uri = new Uri(_baseUri, uri);

            //if (!IsInternalToDomain(uri))
            //    return true;

            if (uri.LocalPath == "/robots.txt")
                return false;

            var userAgentEntry = FindUserAgentEntry(userAgent);
            if (userAgentEntry == null)
                return true;


            string uriParts = uri.LocalPath;
            foreach (var disallowEntry in userAgentEntry.DisallowEntries)
            {
                if (CheckDisallowedEntry(disallowEntry, uriParts))
                {
                    if (CheckExplicitlyAllowed(userAgentEntry, uri))
                        return true;

                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Methods: Crawl-Delay

        public int GetCrawlDelay()
        {
            return GetCrawlDelay(UserAgents.AllAgents);
        }

        public int GetCrawlDelay(string userAgent)
        {
            int crawlDelay = 0;

            var userAgentEntry = FindUserAgentEntry(userAgent);
            if (userAgentEntry == null)
                return crawlDelay;

            if (userAgentEntry.CrawlDelayEntry != null)
            {
                crawlDelay = userAgentEntry.CrawlDelayEntry.CrawlDelay;
            }

            return crawlDelay;
        }

        #endregion

        #region Sitemap

        public IList<string> GetSitemapUrls()
        {
            List<string> sitemapUrls = new List<string>();

            foreach(Entry sitemapEntry in _entries)
            {
                if ( (sitemapEntry is SitemapEntry) && (sitemapEntry != null) )
                    sitemapUrls.Add(((SitemapEntry)sitemapEntry).SitemapUrl);
            }
            
            return sitemapUrls;
        }

        #endregion

        #region Private methods

        private UserAgentEntry FindUserAgentEntry(string userAgent)
        {
            UserAgentEntry allAgentsEntry = null;
            foreach (var entry in _entries)
            {
                var userAgentEntry = entry as UserAgentEntry;
                if (userAgentEntry == null || userAgentEntry.Type != EntryType.UserAgent)
                    continue;

                if (string.Compare(userAgentEntry.UserAgent, userAgent, true) == 0)
                    return userAgentEntry;
                if (string.Compare(userAgentEntry.UserAgent, UserAgents.AllAgents, true) == 0)
                    allAgentsEntry = userAgentEntry;
            }

            return allAgentsEntry;
        }

        private UserAgentEntry FindExplicitUserAgentEntry(string userAgent)
        {
            UserAgentEntry targetEntry = null;
            foreach (var entry in _entries)
            {
                var userAgentEntry = entry as UserAgentEntry;
                if (userAgentEntry == null || userAgentEntry.Type != EntryType.UserAgent)
                    continue;

                if (string.Compare(userAgentEntry.UserAgent, userAgent, true) == 0)
                {
                    targetEntry = userAgentEntry;
                    break;
                }
            }

            return targetEntry;
        }

        private bool CheckExplicitlyAllowed(UserAgentEntry userAgentEntry, Uri uri)
        {
            //if (!IsInternalToDomain(uri))
            //    return true;

            string uriParts = uri.LocalPath;
            foreach (var allowEntry in userAgentEntry.AllowEntries)
            {
                if (CheckAllowedEntry(allowEntry, uriParts))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckAllowedEntry(AllowEntry entry, string uriParts)
        {
            return entry.Regex.IsMatch(uriParts);
        }

        private static bool CheckDisallowedEntry(DisallowEntry entry, string uriParts)
        {
            return entry.Regex.IsMatch(uriParts);
        }

        #endregion

        internal void AddEntry(Entry entry)
        {
            if (entry ==  null)
                throw new ArgumentNullException("entry");
            if (entry.Type != EntryType.Comment && entry.Type != EntryType.UserAgent)
                throw new ArgumentException("Only Comments and User-Agent entries can be added", "entry");

            _entries.Add(entry);
        }

        //This was removed since the _baseUri could change if a.com redirects to b.com. Then what if b.com has links to a.com? With this activated it will always allow because it would consider it external.
        //We check the ParsedWebpageData.IsInternal before calling IsUrlAllowed to be sure it follows our classification of internal and not NRobots
        //private bool IsInternalToDomain(Uri uri)
        //{
        //    return new Link(uri.AbsoluteUri).IsInternalTo(_baseUri.AbsoluteUri);
        //}
    }
}