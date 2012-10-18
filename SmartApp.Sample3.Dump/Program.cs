﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Platform;
using Platform.Messages;
using ServiceStack.ServiceClient.Web;
using ServiceStack.Text;
using SmartApp.Sample3.Contracts;

namespace SmartApp.Sample3.Dump
{
    class Program
    {
        private static IInternalPlatformClient _reader;
        static IEnumerable<string> ReadLinesSequentially(string path)
        {
            using (var rows = File.OpenText(path))
            {
                while (true)
                {
                    var line = rows.ReadLine();
                    if (null != line)
                    {
                        yield return line;
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            var httpBase = string.Format("http://127.0.0.1:8080");
            _reader = new FilePlatformClient(@"C:\LokadData\dp-store", httpBase);
            Thread.Sleep(2000); //waiting for server initialization

            var threads = new List<Task>();
            threads.Add(Task.Factory.StartNew(DumpComments, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness));
            threads.Add(Task.Factory.StartNew(DumpPosts, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness));

            Task.WaitAll(threads.ToArray());
        }

        #region Comments

        private static void DumpComments()
        {
            const string path = @"D:\Temp\Stack Overflow Data Dump - Aug 09\Content\comments.xml";

            long rowIndex = 0;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var commentBytes = new List<byte[]>();
            foreach (var line in ReadLinesSequentially(path).Where(l => l.StartsWith("  <row ")))
            {
                rowIndex++;
                var comment = CommentParse(line);
                if (comment == null)
                    continue;


                commentBytes.Add(comment.ToBinary());

                if (rowIndex % 20000 == 0)
                {
                    _reader.WriteEventsInLargeBatch("s3:comment", commentBytes.Select(x => new RecordForStaging(x)).ToList());
                    Console.WriteLine("Comments:\r\n\t{0} per second\r\n\tAdded {1} posts", rowIndex / sw.Elapsed.TotalSeconds, rowIndex);
                }
            }
        }

        private static Comment CommentParse(string line)
        {
            try
            {
                long defaultLong;
                int defaultInt;
                DateTime defaultDate;

                var comment = new Comment
                               {
                                   Id = long.TryParse(Get(line, "Id"), out defaultLong) ? defaultLong : -1,
                                   PostId = long.TryParse(Get(line, "PostId"), out defaultLong) ? defaultLong : -1,
                                   CreationDate = DateTime.TryParse(Get(line, "CreationDate"), out defaultDate) ? defaultDate : DateTime.MinValue,
                                   Text = HttpUtility.HtmlDecode(Get(line, "Text")),
                                   UserId = long.TryParse(Get(line, "UserId"), out defaultLong) ? defaultLong : -1,
                                   Score = int.TryParse(Get(line, "Score"), out defaultInt) ? defaultInt : -1,
                               };

                return comment;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        private static void DumpPosts()
        {
            const string path = @"D:\Temp\Stack Overflow Data Dump - Aug 09\Content\posts.xml";

            long rowIndex = 0;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            var postBytes = new List<byte[]>();
            foreach (var line in ReadLinesSequentially(path).Where(l => l.StartsWith("  <row ")))
            {
                rowIndex++;
                var post = PostParse(line);
                if (post == null)
                    continue;

                postBytes.Add(post.ToBinary());

                if (rowIndex % 20000 == 0)
                {
                    _reader.WriteEventsInLargeBatch("s3:post", postBytes.Select(x => new RecordForStaging(x)).ToList());
                    Console.WriteLine("Posts:\r\n\t{0} per second\r\n\tAdded {1} posts", rowIndex / sw.Elapsed.TotalSeconds, rowIndex);
                }
            }
        }

        private static Post PostParse(string line)
        {
            try
            {
                long defaultLong;
                DateTime defaultDate;
                var post = new Post
                {
                    Id = long.TryParse(Get(line, "Id"), out defaultLong) ? defaultLong : -1,
                    PostTypeId = long.TryParse(Get(line, "PostTypeId"), out defaultLong) ? defaultLong : -1,
                    CreationDate = DateTime.TryParse(Get(line, "CreationDate"), out defaultDate) ? defaultDate : DateTime.MinValue,
                    ViewCount = long.TryParse(Get(line, "ViewCount"), out defaultLong) ? defaultLong : -1,
                    Body = HttpUtility.HtmlDecode(Get(line, "Body")),
                    OwnerUserId = long.TryParse(Get(line, "OwnerUserId"), out defaultLong) ? defaultLong : -1,
                    LastEditDate = DateTime.TryParse(Get(line, "LastEditDate"), out defaultDate) ? defaultDate : DateTime.MinValue,
                    Title = HttpUtility.HtmlDecode(Get(line, "Title")),
                    AnswerCount = long.TryParse(Get(line, "AnswerCount"), out defaultLong) ? defaultLong : -1,
                    CommentCount = long.TryParse(Get(line, "CommentCount"), out defaultLong) ? defaultLong : -1,
                    FavoriteCount = long.TryParse(Get(line, "FavoriteCount"), out defaultLong) ? defaultLong : -1,
                    Tags = (">" + HttpUtility.HtmlDecode(Get(line, "Tags")) + "<").Split(new[] { "><" }, StringSplitOptions.RemoveEmptyEntries)
                };

                return post;
            }
            catch (Exception)
            {
                return null;
            }

        }

        private static string Get(string line, string attributeName)
        {
            var start = line.IndexOf(attributeName + "=\"");
            var end = line.Substring(start + attributeName.Length + 2).IndexOf("\"");

            if (start == -1 || end == -1)
                return "";

            return line.Substring(start + attributeName.Length + 2, end);
        }
    }


}
