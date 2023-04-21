using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using Sgml;
using NLog;
using MediaPortal.Pbk.Logging;

namespace MediaPortal.Pbk.Net.Http
{
    public class HtmlDocument : XmlDocument
    {
        private static Logger _Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        public HtmlDocument()
            : base()
        {
        }

        /// <summary>
        /// Load from html content.
        /// </summary>
        /// <param name="strContent">html content</param>
        public void LoadFromHtml(string strContent)
        {
            using (TextReader rd = new StringReader(strContent))
            {
                this.LoadFromHtml(rd);
            }
        }

        /// <summary>
        /// Load from html content.
        /// </summary>
        /// <param name="reader">html content</param>
        public void LoadFromHtml(TextReader reader)
        {
            SgmlReader rdSgml = null;
            TextReader rdTxt = null;
            MemoryStream streamMem = null;
            XmlTextReader rdXml = null;
            try
            {
                //_logger.Debug(string.Format("[LoadFromHtml]"));
                // setup SgmlReader
                rdSgml = new SgmlReader();
                rdSgml.DocType = "HTML";
                rdSgml.WhitespaceHandling = WhitespaceHandling.All;
                rdSgml.CaseFolding = CaseFolding.ToLower;
                rdTxt = reader;
                rdSgml.InputStream = rdTxt;
                //sgmlReader.Read();

                // create document

                this.PreserveWhitespace = false;
                this.XmlResolver = null;
                this.Load(rdSgml);

                rdTxt.Close();
                rdTxt.Dispose();
                rdTxt = null;

                // I need to "reload" xml via XmlTextReader to ignore namespace
                streamMem = new MemoryStream();
                this.Save(new XmlTextWriter(streamMem, null));
                streamMem.Position = 0;
                rdXml = new XmlTextReader(streamMem);
                rdXml.Namespaces = false;
                this.Load(rdXml);
            }
            catch (Exception ex)
            {
                _Logger.Error(string.Format("[LoadFromHtml] Error: {0} {1} {2}", ex.Message, ex.Source, ex.StackTrace));
            }
            finally
            {
                if (rdSgml != null)
                    rdSgml.Close();

                if (rdXml != null)
                    rdXml.Close();

                if (streamMem != null)
                {
                    streamMem.Close();
                    streamMem.Dispose();
                    streamMem = null;
                }

                if (rdTxt != null)
                {
                    rdTxt.Close();
                    rdTxt.Dispose();
                    rdTxt = null;
                }
            }

        }
    }
}
