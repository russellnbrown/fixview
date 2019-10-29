using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace FixW
{
    class FixFile
    {

        // public enum FileType { Log, TxN, FIX };

        // private FileType type;
        private String fixFile = "";
        private char[] separator = null;
        private char[] equal = new char[] { '=' };
        private LineTag curTag = null;
        public bool ignoreHB = true;
        public int lastLine = 0;
        private bool running = true;
        public string state = "Initializing";
        private int lineNumber;

        // quickIndex is a map of order id's to order trails. An OrderTrail is a list of lines 
        // relating to an order
        public SortedList<String, OrderTrail> quickIndex = new SortedList<string, OrderTrail>();

        // list of lines - directly from file
        private List<LineTag> lines = new List<LineTag>();
        public List<LineTag> Lines { get { return lines; } }


        public void Stop()
        {
            running = false;
            while (!running)
                System.Threading.Thread.Sleep(10);
        }


        public FixFile(String _txnFile)
        {
            // File to monitor
            fixFile = _txnFile;
 
            // Thread to folow & parse file
            new Thread(new ThreadStart(mon)).Start();
        }

        public string GetStatus()
        {
            return state + " " + fixFile + ", lines:" + lastLine;
        }

        public void mon()
        {
            ignoreHB = true;

            Console.WriteLine("MON for " + fixFile + " starting");
            // Open and 
            state = "Loading";

            StreamReader reader = new StreamReader(new FileStream(fixFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            while (running)
            {
                string line = "";
                while (running && (line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    // create a tag for this line
                    curTag = new LineTag(line);
                    // if it parses ok, add it to lines
                    if (ParseFix())
                        Add(curTag);
                    else
                        curTag = null;
                }
                state = "Following";
                l.Debug("End of block in {0}, size now {1} lines.", fixFile, lastLine);
                System.Threading.Thread.Sleep(50);
            }
            l.Info("MON thread stopped");
            running = true;

        }

        internal void SetOrderFilter(LineTag fl)
        {
            lock (lines)
            {
                foreach (LineTag l in lines)
                {
                    l.hide = true;
                }
            }
            foreach (LineTag l in fl.orderTrail.lines)
            {
                l.hide = false;
            }

        }


        private bool ParseFix()
        {
            // If line is too short, just ignore it
            if (curTag.raw.Length < 80)
                return false;
            // find the fix version tag in the string
            int ix = curTag.raw.IndexOf("8=FIX.4.2", 0, 110);
            // if not found, ignore line
            if (ix == -1)
                return false;
            // if separator is not defined (first run), use the character following the fix format as the separator 
            // ( prob ctrlA but could be something else )
            if (separator == null)
                separator = new char[] { curTag.raw[ix + 9] };
            // split string based on separator
            string[] parts = curTag.raw.Substring(ix).Split(separator);
            // must have at least 2 parts to be anything useful
            if (parts.Length < 2)
                return false;
            // go through the pairs
            foreach (string s in parts)
            {
                if (s.Length < 2) // eol's
                    continue;
                // split pair on '='
                string[] field = s.Split(equal, 2);
                if (field.Length != 2)
                {
                    l.Warn("Error parsing fix field, {0} dosnt split into 2 parts, line# {1}", s, lineNumber);
                    continue;
                }

                // add pair to my map of fields
                try
                {
                    curTag.fields.Add(field[0], field[1]);
                }
                catch (Exception e)
                {
                    l.Error("Duplicate tag in line {1},: {0} ", curTag.raw, lineNumber);
                }
            }
            // everything worked OK
            return true;
        }


        public void AddTrail(LineTag tag)
        {
            // extract relevant order is's
            List<String> idents = new List<string>();
            if (tag.fields.ContainsKey("11")) // new
                idents.Add(tag.fields["11"]);
            if (tag.fields.ContainsKey("41"))
                idents.Add(tag.fields["41"]); // orig

            // if none, nothing to do
            if (idents.Count == 0)
                return;

            // See if we can find an existing order trail using orderid's
            OrderTrail ot = null;
            foreach (String s in idents)
            {
                if (quickIndex.ContainsKey(s))
                {
                    // yes - remember it
                    ot = quickIndex[s];
                    break;
                }
            }

            // no - must be new
            if (ot == null)
                ot = new OrderTrail();


            // add any missing keys to quickindex
            foreach (String s in idents)
            {
                if (!quickIndex.ContainsKey(s))
                {
                    quickIndex[s] = ot;
                    //l.Info("\tadd ident {0} to quick index", s);
                }
            }

            // now add the line to the order trail
            ot.lines.Add(tag); // add this line to trail
            tag.orderTrail = ot;

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


    public class OrderTrail
    {
        public OrderTrail()
        {

        }
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
        public OrderTrail orderTrail = null;
        public Dictionary<String, String> fields = new Dictionary<String, String>();

    }




}
