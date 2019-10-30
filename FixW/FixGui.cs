using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace FixViewer
{

    public class FixFieldConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (value as Line).Field(parameter as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


    public class FixGui :  INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private String filename = "";

        private ObservableCollection<Field> fields = new ObservableCollection<Field>();
        internal ObservableCollection<Field> Fields
        {
            get { return fields; }
            set { fields = value; }
        }

        private ObservableCollection<Line> lines = new ObservableCollection<Line>();
        internal ObservableCollection<Line> Lines
        {
            get { return lines; }
            set { lines = value; }
        }

        private Line selectedLine;
        internal Line SelectedLine { get { return selectedLine; } set { selectedLine = value; UpdateFields(selectedLine); } }

        internal void UpdateFields(Line selected)
        {
            if (selected == null)
                return;

            fields.Clear();
            foreach (var v in selected.tag.fields)
                fields.Add(new Field(FixDictionary.get().GetField(v.Key).name, v.Key, v.Value));
        }

 

    }




    internal class Field
    {
        internal Field(String name, String id, String val)
        {
            this.name = name;
            this.id = id;
            this.value = val;
        }
        private string name = "";
        private string value = "";
        private string id = "";

        internal String Name { get { return name; } }
        internal String Id { get { return id; } }
        internal String Value { get { return value; } }
    }


    internal class Line
    {
        internal Line(LineTag tag)
        {
            this.tag = tag;
        }
        internal LineTag tag = null;


        internal String Field(String id)
        {
            if (tag.fields.ContainsKey(id))
                return tag.fields[id];
            return "";
        }

        public override string ToString()
        {
            return tag.raw;
        }
    }

  

}
