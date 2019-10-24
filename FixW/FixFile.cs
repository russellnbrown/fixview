using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace FixW
{
    class FixFile
    {
        private string pattern = "^.*com\\.db\\.snap\\.messages\\.(\\w+)\\s(.*)";

        public enum FileType { Log, TxN, FIX };

        private FileType type;
        private String txnFile = "";
        private char[] seps = null;
        private char[] equal = new char[] { '=' };
        private LineTag curTag = null;
        public bool ignoreHB = true;
        public int lastLine = 0;
        private bool running = true;
        public string state = "Initializing";
        private int lineNumber;

        // list of lines - directly from file
        private List<LineTag> lines = new List<LineTag>();
        public List<LineTag> Lines {  get { return lines;  } }




        public void Stop()
        {
            running = false;
            while (!running)
                System.Threading.Thread.Sleep(10);
        }

        public string GetTitle()
        {
            return txnFile;
        }

        /*
        com.db.snap.messages.NewOrderSingle { ClOrdID: "AU_ALGO_1507762884334" Currency: "AUD" ExecInst: "5" SecurityIDSource: "101" OrderID: "AU_ALGO_1507762884334" OrderQty { value: 10 scale: 0 } OrdType: "2" Price { value: 8 scale: 0 } SecurityID: "16996927" SenderCompID: "DOS-APAC-AUUAT" Side: "2" TimeInForce: "0" TransactTime: 1507776304013000 SettlType: "0" TradeDate: 20171012 ComplianceID: "171012-DOS-AU_ALGO_1507762884334" Parties { PartyID: "10160" PartyRole: 76 } Parties { PartyID: "=STA2" PartyRole: 24 } CFICode: "ESVUFR" OrderCapacity: "P" LastUpdateTime: 1507776304013000 ManualOrderIndicator: false SrcSysID: "DOS-APAC-AUUAT" ClientAcronym: "=STA2" BusinessArea: "H" CreationTime: 1507776304013000 FlowType: 1 LegalEntity: "DSAL" OrderEntityType: 0 VersionID: "1" OriginationSysID: "DOS-APAC-AUUAT" ParentOrderID: "ALGO_MANUAL-AU_ALGO_1507762884333" Region: "APAC" OriginationFlag: "O" }
        com.db.snap.messages.ExecutionReport { ClOrdID: "AU_ALGO_1507762884334" CumQty { value: 0 scale: 0 } Currency: "AUD" ExecInst: "5" SecurityIDSource: "101" OrderID: "AU_ALGO_1507762884334" OrderQty { value: 10 scale: 0 } OrdStatus: "0" OrdType: "2" Price { value: 8 scale: 0 } SecurityID: "16996927" SenderCompID: "DOS-APAC-AUUAT" Side: "2" TimeInForce: "0" TransactTime: 1507776304018000 SettlType: "0" TradeDate: 20171012 ExecType: "0" LeavesQty { value: 10 scale: 0 } ComplianceID: "171012-DOS-AU_ALGO_1507762884334" Parties { PartyID: "10160" PartyRole: 76 } Parties { PartyID: "=STA2" PartyRole: 24 } Parties { PartyID: "=STA2" PartyRole: 1025 } CFICode: "ESVUFR" OrderCapacity: "P" LastUpdateTime: 1507776304018000 ManualOrderIndicator: false ExecutingTrader: "dbg-russellb" SrcSysID: "DOS-APAC-AUUAT" ClientAcronym: "=STA2" BusinessArea: "H" CreationTime: 1507776304013000 FlowType: 1 LegalEntity: "DSAL" NotionalValue { value: 0 scale: 0 } OrderEntityType: 0 VersionID: "2" OriginationSysID: "DOS-APAC-AUUAT" ParentOrderID: "ALGO_MANUAL-AU_ALGO_1507762884333" Region: "APAC" OriginationFlag: "O" }
        */

        // initial instance has no data
        public FixFile(FileType t)
        {
            txnFile = "";
            type = t;
        }


        public FixFile(String _txnFile, FileType t)
        {
            // File to monitor
            txnFile = _txnFile;
            type = t;

            // Thread to folow & parse file
            new Thread(new ThreadStart(mon)).Start();
        }

        public void mon()
        {
            ignoreHB = true;// Settings.get().ignoreHB;

            Console.WriteLine("MON for " + txnFile + " starting");

            // wait till file is created 
            while (running)
            {

                if (!File.Exists(txnFile))
                {
                    state = "Waiting for " + txnFile;
                    Thread.Sleep(1000);
                    Console.WriteLine("Waiting for " + txnFile + " starting");
                    continue;
                }
                Console.WriteLine("File found: " + txnFile);
                break;
            }

            if (!running)
                return;

            Console.WriteLine("Opened for " + txnFile + " starting");
            // Open and 
            state = "Loading " + txnFile;

            StreamReader reader = new StreamReader(new FileStream(txnFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            while (running)
            {
                string line = "";
                while (running && (line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    curTag = new LineTag(line);
                    if (Parse())
                    {
                        Add(curTag);
                    }
                    else
                        curTag = null;

                }
                l.Debug("End of block in {0}, size now {1} lines.", txnFile, lastLine);
                state = "Loaded";// Print();
                System.Threading.Thread.Sleep(500);
            }
            l.Info("MON thread stopped");
            running = true;

        }

        internal void SetOrderFilter(Line fl)
        {
            foreach(LineTag l in lines)
            {

            }
        }

        private bool Parse()
        {
            switch (type)
            {
                case FileType.TxN:
                    return ParseTxN();
                case FileType.FIX:
                    return ParseFix();
                case FileType.Log:
                    return true;
            }
            return false;
        }

        private bool ParseTxN()
        {
            return false;
        }

        private bool ParseFix()
        {
            if (curTag.raw.Length < 80)
                return false;
            int ix = curTag.raw.IndexOf("8=FIX.4.2", 0, 110);
            if (ix == -1)
                return false;
            if (seps == null)
                seps = new char[] { curTag.raw[ix + 9] };
            string[] parts = curTag.raw.Substring(ix).Split(seps);
            if (parts.Length == 0)
                return false;
            foreach (string s in parts)
            {
                if (s.Length < 2) // eol's
                    continue;

                string[] field = s.Split(equal, 2);
                if (field.Length != 2)
                {
                    l.Warn("Error parsing fix field, {0} dosnt split into 2 parts, line# {1}", s, lineNumber);
                    continue;
                }


                try
                {
                    curTag.fields.Add(field[0], field[1]);
                }
                catch(Exception e)
                {
                    l.Error("Duplicate tag in line {1},: {0} ", curTag.raw, lineNumber);
                }
            }
            return true;
        }



  
        // lines related to a particular order
        public List<OrderThread> threads = new List<OrderThread>();
        // map of id -> thread
        public SortedList<String, OrderThread> quickIndex = new SortedList<string, OrderThread>();



        public void AddTrail(LineTag tag)
        {
            List<String> idents = new List<string>();
 
            if (tag.fields.ContainsKey("11")) // new
                idents.Add(tag.fields["11"]);
            if (tag.fields.ContainsKey("41"))
                idents.Add(tag.fields["41"]); // orig

            if (idents.Count == 0)
                return;

            String allTags = "";
            idents.ForEach(item => allTags += (item + ",") );

           // l.Info("Adding line with tags {0}", allTags);

            // See if we can find thread using keys
            OrderThread ot = null;
            foreach(String s in idents)
            {
                if (quickIndex.ContainsKey(s))
                {
                    ot = quickIndex[s];
                    //l.Info("\tFound quick index based on " + s);
                    break;
                }
            }

            // no - must be new
            if (ot == null)
            {
                //l.Info("\tNothing found in quick index, create new");
                ot = new OrderThread();
            }

            // add any missing keys to quickindex
            foreach (String s in idents)
            {
                if (!quickIndex.ContainsKey(s))
                {
                    quickIndex[s] = ot;
                    //l.Info("\tadd ident {0} to quick index", s);
                }
            }

            ot.lines.Add(tag); // add this line to trail
            tag.thread = ot;



        }

        public void Add(LineTag tag)
        {
            if (tag.fields.ContainsKey("35"))
                tag.type = tag.fields["35"];

            if (tag.type == "0" && ignoreHB)
                return;

            lock (lines)
            {
                lines.Add(tag);
                lastLine++;
            }

  
            AddTrail(tag);

        }
        


    }


    public class OrderThread
    {
        public OrderThread()
        {

        }
        //public List<String> alias = new List<String>();
        public List<LineTag> lines = new List<LineTag>();
    }

    public class LineTag 
    {
        public LineTag(string l)
        {
            raw = l;
        }
        public string raw;
        public string type;
        public string id;
        public bool hide = false;
        public OrderThread thread = null;
        public Dictionary<String, String> fields = new Dictionary<String, String>();

        /*public string GetValue(LineColumn lc)
        {
            if (fields.ContainsKey(lc.ident))
                return fields[lc.ident];
            return "";
        }*/
    }




}
