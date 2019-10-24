using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace FixW
{
    public class FixDictionary
    {
        public static FixDictionary get() { return instance; }
        private static FixDictionary instance = null;

        [Serializable]
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


        public bool Load(String inputUrl)
        {
            if (!File.Exists(inputUrl))
            {
                return false;
            }
            try
            {
                using (XmlReader reader = XmlReader.Create(inputUrl))
                {
                    reader.ReadToFollowing("fields");

                    while (reader.Read())
                    {
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
