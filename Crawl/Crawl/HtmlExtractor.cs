//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.DecisionService.Crawl.Data;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Microsoft.DecisionService.Crawl 
{
    /// <summary>
    /// https://moz.com/blog/meta-data-templates-123
    /// </summary>
    public static class HtmlExtractor
    {
        private static readonly HashSet<string> TitleProperties;
        private static readonly HashSet<string> DescriptionProperties;

        static HtmlExtractor()
        {
            TitleProperties = new HashSet<string>
            { "og:title", "twitter:title" };

            DescriptionProperties = new HashSet<string>
            { "og:description", "twitter:description" };
        }

        private static string FirstOrNull(HtmlNodeCollection collection, HashSet<string> properties)
        {
            var node = collection.First(n => properties.Contains(n.Attributes["property"].Name));
            return node != null ? node.Attributes["content"].Value : null;
        }

        private static string FindMeta(HtmlNode headNode, string xpath)
        {
            var nodes = headNode.SelectNodes(xpath);
            if (nodes == null)
                return null;

            foreach (var node in nodes)
            {
                var attr = node.Attributes["content"];
                if (attr != null)
                    return attr.Value;

                attr = node.Attributes["value"];
                if (attr != null)
                    return attr.Value;
            }

            return null;
        }

        private static string FindValue(HtmlNode headNode, string xpath)
        {
            var nodes = headNode.SelectNodes(xpath);
            if (nodes == null)
                return null;

            foreach (var node in nodes)
            {
                var title = new StringBuilder();
                StripTags(node, title);

                if (title.Length > 0)
                    return title.ToString();
            }

            return null;
        }


        private static IEnumerable<string> FindAll(HtmlNode headNode, string xpath)
        {
            var nodes = headNode.SelectNodes(xpath);
            if (nodes == null)
                yield break;

            foreach (var node in nodes)
            {
                var attr = node.Attributes["content"];
                if (attr != null)
                    yield return attr.Value;

                attr = node.Attributes["value"];
                if (attr != null)
                    yield return attr.Value;
            }
        }

        private static HashSet<string> skipTags = new HashSet<string>()
        {
            "script", "style"
        };

        private static void StripTags(HtmlNode root, StringBuilder plaintext)
        {
            foreach (var node in root.ChildNodes)
            {
                if (skipTags.Contains(node.Name.ToLowerInvariant()) || node.NodeType == HtmlNodeType.Comment)
                    continue;

                if (!node.HasChildNodes)
                {
                    string text = node.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                        plaintext.Append(text.Trim()).Append(' ');
                }
                else
                    StripTags(node, plaintext);
            }
        }

        public static string StripTags(HtmlNode root)
        {
            var plaintext = new StringBuilder();

            StripTags(root, plaintext);

            return plaintext.ToString();
        }

        public static CrawlResponse Parse(string html, Uri sourceUrl)
        {
            var response = new CrawlResponse();
                    
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var head = doc.DocumentNode.SelectSingleNode("html/head");
            if (head == null)
                return response;

            response.Title = FindMeta(head, "meta[@property='og:title' or name='og:title' or @property='twitter:title' or @name='twitter:title']");

            if (string.IsNullOrEmpty(response.Title))
                response.Title = FindValue(head, "title");

            if (!string.IsNullOrEmpty(response.Title))
                response.Title = WebUtility.HtmlDecode(response.Title.Trim());

            response.Description = FindMeta(head, "meta[@property='og:description' or name='og:description' or @property='twitter:description' or @name='twitter:description' or @name='description']");

            if (string.IsNullOrEmpty(response.Description))
                response.Title = FindValue(head, "title");

            if (response.Description != null)
                response.Description = WebUtility.HtmlDecode(response.Description.Trim());

            response.Type = FindMeta(head, "meta[@property='og:type' or name='og:type']");
            var categories = FindAll(head, "meta[@property='article:tag' or @name='article:tag']").ToList();
            if (categories.Count > 0)
                response.Categories = categories;

            // TODO: get the better resolution
            var img = FindMeta(head, "meta[@property='og:image' or name='og:image' or @property='twitter:image' or @name='twitter:image']");
            if (img != null)
            {
                if (img.StartsWith("//"))
                    img = sourceUrl.Scheme + ":" + img;

                // TODO: support relative URLs too
                response.Image = img;
            }

            // build article
            var articleText = new StringBuilder();

            var articles = doc.DocumentNode.SelectNodes("//article");

            if (articles != null)
            {
                // find the longest article text
                string text = null;
                foreach (var art in articles)
                {
                    var newText = StripTags(art);
                    if (text == null || text.Length < newText.Length)
                        text = newText;
                }

                if (!string.IsNullOrEmpty(text))
                    articleText.AppendLine(text);
            }

            response.Article = WebUtility.HtmlDecode(articleText.ToString());
            
            // <meta property="microsoft:ds_id" content="255308" data-react-helmet="true">
            var dsId = FindMeta(head, "meta[@property='microsoft:ds_id' or name='microsoft:ds_id']");
            response.PassThroughDetails = WebUtility.HtmlDecode(dsId);

            return response;
        }
    }

}
