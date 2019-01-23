using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace wechatart2markdown.csharp
{

    public class WechatArt2Md
    {

        private const int blockHeight = 3;    // 行快大小(方向向下）
        private const int threshold = 150;  // 阈值

        private string html;         // 网页源码
        private int textStart;       // 网页正文开始行数
        private int textEnd;         // 网页正文结束行数
        private string textBody;     // 提取到的<body>标签内的内容
        private string[] lines;      // 按行存储textBody的内容
        private List<int> blockLen;  // 每个行快的总字数

        public string content;       // 提取到的网页正文
        public string title;         // 网页标题
        public string webPreview;    // 预览页面（去除JS和图片）
        public string mdContent;
        public string pdfContent;
        private bool bJoinMethond;   // ture: 拼接, false: 直接提取

        // 隐藏默认构造函数
        private WechatArt2Md()
        {
        }

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="url"></param>
        /// <param name="bMethond"></param>
        public WechatArt2Md(string url, bool bMethond)
        {

            html = GetHTML(url, Encoding.UTF8);

            textStart = 0;
            textEnd = 0;
            textBody = "";
            blockLen = new List<int>();
            title = "";
            content = "";
            webPreview = "";

            bJoinMethond = bMethond;

            extract();
        }

        // 提取网页正文
        public void extract()
        {
            extractTitle();    // 提取标题
            extractBody();     // 提取<body>标签中的内容
            removeTags();      // 去除textBody中的HTML标签
            extractText();     // 提取网页正文,根据bJoinMethond选择不同的方法
            convert2MarkDown();// 网页正文转换为markdown
            extractPreview();  // 提取预览页面的HTML代码（去除图片和JS）
        }

        private void extractTitle()
        {
            string pattern = @"(?is)<title>(.*?)</title>";
            Match m = Regex.Match(html, pattern);
            if (m.Success)
            {
                title = m.Groups[1].Value;
                title = Regex.Replace(title, @"(?is)\s+", " ").Trim();
            }
        }

        private void extractBody()
        {
            string pattern = @"(?is)<body.*?</body>";
            Match m = Regex.Match(html, pattern);
            if (m.Success)
                textBody = m.ToString();
        }

        private void removeTags()
        {
            string docType = @"(?is)<!DOCTYPE.*?>";
            string crlf = @"(?is)</p>|<br.*?/>";
            string comment = @"(?is)<!--.*?-->";
            string js = @"(?is)<script.*?>.*?</script>";
            string css = @"(?is)<style.*?>.*?</style>";
            string specialChar = @"&.{2,8};|&#.{2,8};";
            //string otherTag    = @"(?is)<.*?>";
            string otherTag = @"(?is)<(?![img]).*?>";

            textBody = Regex.Replace(textBody, docType, "");
            textBody = Regex.Replace(textBody, crlf, "[crlf]");
            textBody = Regex.Replace(textBody, comment, "");
            textBody = Regex.Replace(textBody, js, "");
            textBody = Regex.Replace(textBody, css, "");
            textBody = Regex.Replace(textBody, specialChar, "");
            textBody = Regex.Replace(textBody, otherTag, "");
        }

        private void extractText()
        {
            // 将连续的空白符替换为单个空格
            // 并去除每行首尾的空白符
            lines = textBody.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = Regex.Replace(lines[i], @"(?is)\s+", " ").Trim();

            // 去除上下紧邻行为空,且该行字数小于30的行
            for (int i = 1; i < lines.Length - 1; i++)
            {
                if (lines[i].Length > 0 && lines[i].Length < 30
                    && 0 == lines[i - 1].Length && 0 == lines[i + 1].Length)
                    lines[i] = "";
            }

            // 统计去除空白字符后每个行块所含总字数
            for (int i = 0; i < lines.Length - blockHeight; i++)
            {
                int len = 0;
                for (int j = 0; j < blockHeight; j++)
                    len += lines[i + j].Length;
                blockLen.Add(len);
            }

            // 寻找各个正文块起始和结束行,并进行拼接
            textStart = FindTextStart(0);

            if (0 == textStart)
                content = "未能提取到正文!";
            else
            {
                if (bJoinMethond)
                {
                    while (textEnd < lines.Length)
                    {
                        textEnd = FindTextEnd(textStart);
                        content += GetText();
                        textStart = FindTextStart(textEnd);
                        if (0 == textStart)
                            break;
                        textEnd = textStart;
                    }
                }
                else
                {
                    textEnd = FindTextEnd(textStart);
                    content += GetText();
                }
            }

        }

        // 如果一个行块大小超过阈值,且紧跟其后的1个行块大小不为0,则此行块为起始点（即连续的4行文字长度超过阈值）
        private int FindTextStart(int index)
        {
            for (int i = index; i < blockLen.Count - 1; i++)
            {
                if (blockLen[i] > threshold && blockLen[i + 1] > 0)
                    return i;
            }
            return 0;
        }

        // 起始点之后,如果2个连续行块大小都为0,则认为其是结束点（即连续的4行文字长度为0）
        private int FindTextEnd(int index)
        {
            for (int i = index + 1; i < blockLen.Count - 1; i++)
            {
                if (0 == blockLen[i] && 0 == blockLen[i + 1])
                    return i;
            }
            return lines.Length - 1;
        }

        private string GetText()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = textStart; i < textEnd; i++)
            {
                if (lines[i].Length != 0)
                    sb.Append(lines[i]).Append("\n\n");
            }
            return sb.ToString();
        }

        private void extractPreview()
        {
            webPreview = Regex.Replace(html, @"(?is)<[^>]*jpg.*?>", "");
            webPreview = Regex.Replace(webPreview, @"(?is)<[^>]*gif.*?>", "");
            webPreview = Regex.Replace(webPreview, @"(?is)<[^>]*png.*?>", "");
            webPreview = Regex.Replace(webPreview, @"(?is)<[^>]*js.*?>", "");
            webPreview = Regex.Replace(webPreview, @"(?is)<script.*?>.*?</script>", "");
        }

        private void convert2MarkDown()
        {

            //处理换行
            mdContent = content.Replace("[crlf]", "\n\n");

            mdContent = Regex.Replace(mdContent, @"(?is)<img.*?>", replaceMdImg);

            removeHiddenText();

            removeBadTags();
        }

        private static string replaceMdImg(Match match)
        {
            string sub_pattern = @"(?is)<img.*?data-src=""(.*?)"".*?>";
            string sub_match = Regex.Replace(match.Value, sub_pattern, convertMdImg);

            sub_pattern = @"(?is)<img.*?src=""(.*?)"".*?>";
            return Regex.Replace(sub_match, sub_pattern, convertMdImg);
        }

        //先将img链接改成md图片格式
        private static string convertMdImg(Match m)
        {
            return "![a](" + m.Groups[1].Value + ")";
        }

        private void removeHiddenText()
        {

            List<string> hiddenTextList = new List<string>();

            hiddenTextList.Add(@"<img class=""reward_qrcode_img"" src=""//res.wx.qq.com/mmbizwap/zh_CN/htmledition/images/pic/appmsg/pic_reward_qrcode.2x42f400.png"">");

            hiddenTextList.Add("受苹果公司新规定影响，微信 iOS 版的赞赏功能被关闭，可通过二维码转账支持公众号。");
            hiddenTextList.Add("<br/>");
            hiddenTextList.Add("<br />");

            hiddenTextList.ForEach(c => mdContent = mdContent.Replace(c, ""));
            mdContent = mdContent.Trim("\n".ToCharArray());
        }

        private void removeBadTags()
        {

            List<string> tagList = new List<string>();

            tagList.Add("(?is)<iframe.*?>");

            tagList.ForEach(c => mdContent = Regex.Replace(mdContent, c, ""));
        }

        public static string GetHTML(string url, Encoding en)
        {

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "*/*";
            request.Headers.Add("Accept-Encoding", ""); // 禁止压缩
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1)";

            //服务器返回的内容
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            if (response != null && HttpStatusCode.OK == response.StatusCode)
            {
                StreamReader sr = new StreamReader(response.GetResponseStream(), en);
                return sr.ReadToEnd();
            }

            return "";
        }

    }
}
