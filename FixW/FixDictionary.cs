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
using System.IO;
using System.Xml;

namespace FixViewer
{
    /*FixDictionary
    * FixDictionary is used to read and parse a standard fix dictionary file. It simply
    * maintains a map of field/message numbers to names
    */

    public class FixDictionary
    {
        // We only have the one, use a singleton for easy access
        public static FixDictionary get() { return instance; }
        private static FixDictionary instance = null;

        public class Field
        {
            public Field(string ident, string name, string type)
            {
                this.name = name;
                this.ident = ident;
                this.type = type;
            }
            public string name = "";
            public string ident = "";
            public string type = "";
        }

        private SortedDictionary<String, Field> fields = new SortedDictionary<String, Field>();

        internal SortedDictionary<string, Field> Fields
        {
            get
            {
                return fields;
            }
        }

        public string SafeName(String id)
        {
            if (Fields.ContainsKey(id))
                return Fields[id].name;
            return id;
        }

        public Field GetField(String id)
        {
            if (!Fields.ContainsKey(id))
            {
                l.Error("Field " + id + " not in fix dictionary");
                Fields.Add(id, new Field(id, id + "_missing", "STRING"));
            }
            return Fields[id];
        }

        public String GetId(String name)
        {
            foreach(var f in Fields)
            {
                if (f.Value.name == name)
                    return f.Value.ident;
            }
            l.Fatal("Can't find Id for Name {0} in FixDictionary", name);
            return "";
        }

        public Field GetField(String id, String name)
        {
            if (Fields.ContainsKey(id))
                return Fields[id];
            fields.Add(id, new Field(id, name, "STRING"));
            return Fields[id];
        }

        public FixDictionary()
        {
            instance = this;
        }

        /* Load
        *  Loads the dictionary XML file and extract fields, names and types
        */
        public bool Load(String inputUrl)
        {
            if (!File.Exists(inputUrl))
                return false;

            try
            {
                // open file & find 'fields' section
                using (XmlReader reader = XmlReader.Create(inputUrl))
                {
                    reader.ReadToFollowing("fields");

                    while (reader.Read())
                    {
                        // read field elements extracting the bits we need
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                if (reader.Name == "field")
                                {
                                    string ident = reader.GetAttribute("number");
                                    string name = reader.GetAttribute("name");
                                    string type = reader.GetAttribute("type");
                                    l.Debug("   FLD " + name + ", id=" + ident + ", type=" + type + ", has values =" + (reader.IsEmptyElement ? "Yes" : "No"));
                                    fields.Add(ident, new Field(ident, name, type));
                                }
                                else
                                    l.Debug("   el=" + reader.Name + " " + reader.Value);
                                break;
                            case XmlNodeType.Text:
                                break;
                            case XmlNodeType.EndElement:
                                break;
                        }
                    }
                    return true;

                }
            }
            catch (SystemException se)
            {
                l.Error(se.Message);

            }
            return false;
        }


    }
}
