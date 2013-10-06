using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using DebugDiag.DotNet;
using DebugDiag.DotNet.AnalysisRules;

namespace Winterdom.DebugDiag.AnalysisRules
{
    public class SPListAnalysis : IHangDumpRule, IAnalysisRuleMetadata
    {
        private NetScriptManager manager;
        private NetDbgObj debugger;
        private NetProgress progress;
        private Dictionary<int, int> threadsWithLargeSPQueries;
        private List<int> threadsWithStarSPQueries;
        private List<int> threadsWithNoRowFilter;
        private int currentThread;

        const String SPLIST_FILL_FRAME = "Microsoft.SharePoint.SPListItemCollection.EnsureListItemsData";
        const int MAX_VIEWFIELDS = 10;

        public string Category
        {
            get { return "Performance Analyzers"; }
        }

        public string Description
        {
            get { return "Analyzes SPList queries in SharePoint"; }
        }

        public void RunAnalysisRule(NetScriptManager manager, NetDbgObj debugger, NetProgress progress)
        {
            this.manager = manager;
            this.debugger = debugger;
            this.progress = progress;
            this.threadsWithLargeSPQueries = new Dictionary<int, int>();
            this.threadsWithStarSPQueries = new List<int>();
            this.threadsWithNoRowFilter = new List<int>();
            this.currentThread = 0;

            this.progress.SetOverallRange(0, 2);

            this.manager.WriteLine("<h1>" + HttpUtility.HtmlEncode(debugger.DumpFileShortName) + "</h1>");

            RunSPListAnalysis(debugger);
        }

        private void RunSPListAnalysis(NetDbgObj debugger)
        {
            this.progress.OverallPosition = 1;
            this.progress.OverallStatus = "Analyzing threads";
            this.progress.SetCurrentRange(0, debugger.Threads.Count);

            foreach ( var thread in debugger.Threads )
            {
                AnalyzeThread(thread);
            }

            this.progress.OverallPosition = 2;
            this.progress.OverallStatus = "Generating Report";
            ReportThreadsWithLargeSPQueries();
            ReportThreadsWithStarSPQueries();
            ReportThreadsWithNoRowFilter();
        }

        private void ReportThreadsWithNoRowFilter()
        {
            if ( threadsWithNoRowFilter.Count > 0 )
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("The following threads appear to be executing SPList queries with no RowFilter specified.");
                sb.Append("<br/>");
                foreach ( int threadId in threadsWithNoRowFilter )
                {
                    sb.AppendFormat("<a href='#thread{0}'>{0}</a>, ", threadId);
                }
                sb.Remove(sb.Length - 2, 2);
                this.manager.ReportWarning(sb.ToString(), "");
            }
        }

        private void ReportThreadsWithStarSPQueries()
        {
            if ( threadsWithStarSPQueries.Count > 0 )
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("The following threads appear to be executing SPList queries requesting ALL fields.");
                sb.Append("<br/>");
                foreach ( int threadId in threadsWithStarSPQueries )
                {
                    sb.AppendFormat("<a href='#thread{0}'>{0}</a>, ", threadId);
                }
                sb.Remove(sb.Length - 2, 2);
                this.manager.ReportWarning(sb.ToString(), "");
            }
        }

        private void ReportThreadsWithLargeSPQueries()
        {
            if ( threadsWithLargeSPQueries.Count > 0 )
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("The following threads appear to be executing SPList queries requesting many fields.");
                sb.Append("<br/><ul>");
                foreach ( int threadId in threadsWithLargeSPQueries.Keys )
                {
                    sb.AppendFormat("<li><a href='#thread{0}'>{0}</a> ({1} fields)</li>",
                        threadId, threadsWithLargeSPQueries[threadId]);
                }
                sb.Append("</ul>");
                this.manager.ReportWarning(sb.ToString(), "");
            }
        }

        private void AnalyzeThread(NetDbgThread thread)
        {
            this.progress.CurrentPosition = ++this.currentThread;
            this.progress.CurrentStatus = "Analyzing Thread " + thread.ThreadID;
            if ( ContainsFrame(thread, SPLIST_FILL_FRAME) )
            {
                this.manager.WriteLine(String.Format("<a id='thread{0}'><h3>Thread {0}</h3></a>", thread.ThreadID));
                dynamic obj = thread.FindFirstStackObject("Microsoft.SharePoint.SPListItemCollection");
                if ( obj != null )
                {
                    String viewXml = (string)obj.m_Query.m_strViewXml;
                    if ( String.IsNullOrEmpty(viewXml) )
                    {
                        viewXml = "<View><Query>" + (string)obj.m_Query.m_strQuery + "</Query></View>";
                    }
                    if ( !String.IsNullOrEmpty(viewXml) )
                    {
                        XDocument doc = XDocument.Parse(viewXml);
                        AnalyzeViewXml(thread, doc);
                        this.manager.WriteLine("SPQuery: <pre>" + HttpUtility.HtmlEncode(doc.ToString()) + "</pre>");
                    }
                }
                PrintThreadStack(thread);
            }
        }

        private void PrintThreadStack(NetDbgThread thread)
        {
            this.manager.WriteLine("<pre>");
            foreach ( var frame in thread.ManagedStackFrames )
            {
                String line = String.Format("{0:x16}  {1}", 
                    (ulong)frame.InstructionAddress,
                    HttpUtility.HtmlEncode(frame.FunctionName));

                if ( line.Contains(SPLIST_FILL_FRAME) )
                {
                    this.manager.WriteLine("<font color='red'>" + line + "</font>");
                } else
                {
                    this.manager.WriteLine(line);
                }
            }
            this.manager.WriteLine("</pre>");
        }

        private void AnalyzeViewXml(NetDbgThread thread, XDocument doc)
        {
            var view = doc.Root;
            var viewFields = view.Element("ViewFields");
            if ( viewFields != null )
            {
                int numFields = viewFields.Elements().Count();
                if ( numFields > MAX_VIEWFIELDS )
                {
                    this.threadsWithLargeSPQueries.Add(thread.ThreadID, numFields);
                }
                this.manager.Write("<table border='1'><tr><td>Fields</td><td>");
                foreach ( var viewField in viewFields.Elements("FieldRef") )
                {
                    this.manager.WriteLine(String.Format("{0}", viewField.Attribute("Name").Value));
                }
                this.manager.WriteLine("</td></tr></table>");
            } else
            {
                this.threadsWithStarSPQueries.Add(thread.ThreadID);
            }
            if ( view.Element("RowLimit") == null )
            {
                this.threadsWithNoRowFilter.Add(thread.ThreadID);
            }
        }

        private bool ContainsFrame(NetDbgThread thread, string name)
        {
            foreach ( var frame in thread.ManagedStackFrames )
            {
                if ( frame.FunctionName.Contains(name) )
                    return true;
            }
            return false;
        }
    }
}
