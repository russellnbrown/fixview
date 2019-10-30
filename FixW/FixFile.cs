/*
 * Copyright (C) 2019 russell brown
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;

namespace FixViewer
{
    /*
     * FixFile  - this opens and parses a fix file, the parsing is done on a seperate thread. Processed lines 
     * are put into a 'LineTag' and placed in the 'lines' list. This is subsequently read by the main GUI thread 
     * and lines are removed and stored in the local GUI list.
     */

    class FixFile
    {

        private String fixFile = "";
        private char[] separator = null;
        private char[] equal = new char[] { '=' };
        private LineTag curTag = null;
        private bool ignoreHB = true;
        private int lastLine = 0;
        private bool running = true;
        private string state = "Initializing";
        private int lineNumber;
        private Thread readThread = null;
        private const string fixTag = "8=FIX.4."; // only handle FIX4.X at the moment

        // quickIndex is a map of order id's to order trails. An OrderTrail is a list of lines 
        // relating to an order
        private SortedList<String, OrderTrail> quickIndex = new SortedList<string, OrderTrail>();

        // list of lines - directly from file - adding/removing to this is protected by locking as we add to the
        // list in our 'mon' thread and remove in the main GUI thread.
        private List<LineTag> lines = new List<LineTag>();
        internal List<LineTag> Lines { get { return lines; } }

        // Stop - called from main thread. running is set to false which will cause
        // the readThread to stop & exit
        public void Stop()
        {
            running = false;
            readThread.Join();
        }

        // FixFile constructor
        // start the read thread
        public FixFile(String _fixFile)
        {
            // File to monitor
            fixFile = _fixFile;

            // Thread to folow & parse file
            readThread = new Thread(new ThreadStart(readFixFile));
            readThread.Start();
        }

        // GetStatus - returns a status string that reflects what we are doing, what
        // we are reading & how many lines we have read
        public string GetStatus()
        {
            return state + " " + fixFile + ", lines:" + lastLine;
        }

        // the file reader thread
        private void readFixFile()
        {
            ignoreHB = true; // ignore heartbeats

            state = "Loading";

            // Open file & enter main loop. when running is set to false we will stop
            StreamReader reader = new StreamReader(new FileStream(fixFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            while (running)
            {
                string line = "";
                // read all available lines, when we get to the end we will just repeat 
                // until there is more to read
                while (running && (line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    // create a tag for this line, initially it just holds the lines text
                    curTag = new LineTag(line);
                    // parse it splitting it into tag/value pairs, if it parses ok, add it to lines
                    if (parseFix())
                        add(curTag);
                    else
                        curTag = null;
                }
                // reached end of data, set new state & wait for more to appear
                state = "Following";
                l.Debug("End of block in {0}, size now {1} lines.", fixFile, lastLine);
                System.Threading.Thread.Sleep(50); // teeny sleep so we dont hog processor
            }
            l.Info("MON thread stopped");
            running = true;
        }


        private bool parseFix()
        {
            // If line is too short, just ignore it
            if (curTag.raw.Length < 80)
                return false;
            // find the fix version tag in the string
            int ix = curTag.raw.IndexOf(fixTag, 0, 110);
            // if not found, ignore line
            if (ix == -1)
                return false;
            // if separator is not defined (first run), use the character following the fix format as the separator 
            // ( prob ctrlA but could be something else )
            if (separator == null)
                separator = new char[] { curTag.raw[ix + fixTag.Length+1] };
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

        

        // add - adds a line to the line list
        private void add(LineTag tag)
        {
            // extract order type and save seperatly
            if (tag.fields.ContainsKey("35"))
                tag.type = tag.fields["35"];

            // if its a heartbeat and set to ignore them, just return here
            if (tag.type == "0" && ignoreHB)
                return;

            // add the line to the lines list. We need to lock the list as it is shared by GUI thread
            lock (lines)
            {
                lines.Add(tag);
                lastLine++;
            }

            // add to order trail
            addTrail(tag);

        }

        // addTrail - if the message relates to an order, add to the list of
        // messages for that order
        private void addTrail(LineTag tag)
        {
            // extract relevant order is's (Cloid,OrigCloid)
            List<String> idents = new List<string>();
            if (tag.fields.ContainsKey("11")) // new
                idents.Add(tag.fields["11"]);
            if (tag.fields.ContainsKey("41"))
                idents.Add(tag.fields["41"]); // orig

            // if none, nothing to do
            if (idents.Count == 0)
                return;

            // See if we can find an existing order trail using orderid's quickIndex
            // is used to map an order id to an order trail
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


            // add any missing keys to quickindex and point to the order trail
            foreach (String s in idents)
            {
                if (!quickIndex.ContainsKey(s))
                    quickIndex[s] = ot;
            }

            // add the new line to the order trail
            ot.lines.Add(tag);

            // and point to the order trail from the line
            tag.orderTrail = ot;

        }

        // Called from main GUI, passes us a line, hide all lines not relevant to the order
        // and unhide relevant ones using the order trail
        internal void SetOrderFilter(LineTag fl)
        {
            // Hide all lines
            lock (lines)
            {
                foreach (LineTag l in lines)
                {
                    l.hide = true;
                }
            }
            // unhide lines in the order trail
            foreach (LineTag l in fl.orderTrail.lines)
            {
                l.hide = false;
            }

        }



    }

    // OrderTrail - class to hold list of lines forming the order trail
    internal class OrderTrail
    {
        internal List<LineTag> lines = new List<LineTag>();
    }

    // LineTag - class to hold relevant information for a line in the fix file
    internal class LineTag
    {
        // LineTag - constructor. saves a copy of the raw line data
        internal LineTag(string l)
        {
            raw = l;
        }
        internal string raw;
        internal string type;
        internal string id;
        internal bool hide = false;
        internal OrderTrail orderTrail = null;
        internal Dictionary<String, String> fields = new Dictionary<String, String>();

    }




}
