using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FixW
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 


    public partial class MainWindow : Window
    {
        private FixGui fixGui = new FixGui();
        private FixDictionary fdict;
        private FixFile ff = null;



        public MainWindow()
        {
            l.MinLogLevel = l.Level.Debug;
            l.MinConsoleLogLevel = l.Level.Info;
            l.To("fixw.log");

            fdict = new FixDictionary();
            if (!fdict.Load(ConfigurationManager.AppSettings["FIXDICTIONARY"]))
                l.Fatal("Can't load dictionary");

            DataContext = fixGui;
            InitializeComponent();
            
            GridView myGridView = new GridView();
            myGridView.AllowsColumnReorder = true;
            myGridView.ColumnHeaderToolTip = "Fix Fields";

            NameValueCollection tsection = ConfigurationManager.GetSection("COLUMNS") as NameValueCollection;
            foreach (string key in tsection)
            {
                string attr = tsection[key];
                GridViewColumn gvc1 = new GridViewColumn();
                Binding b = new Binding();
                b.Path = new PropertyPath(".");
                b.Converter = new FixFieldConverter();
                int id = -1;
                if ( Int32.TryParse(key, out id ))
                    b.ConverterParameter = id.ToString();
                else
                    b.ConverterParameter = fdict.GetId(key);
                gvc1.DisplayMemberBinding = b;
                gvc1.Header = key;
                gvc1.Width = Int32.Parse(attr);
                myGridView.Columns.Add(gvc1);
            }
            
            
            listView.View = myGridView;



            this.Title = ConfigurationManager.AppSettings["FILE"];
            ff = new FixFile(ConfigurationManager.AppSettings["FILE"], FixFile.FileType.FIX);

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += OnTimer;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            dispatcherTimer.Start();

        }

        private void OnTimer(object sender, EventArgs e)
        {
            // any lines to copy in
            int addedLines = 0;
            lock(ff.Lines)
            {
                while(fixGui.Lines.Count < ff.Lines.Count  )
                {
                    LineTag lt = ff.Lines[fixGui.Lines.Count];
                    Line ln = new Line(lt);
                    fixGui.Lines.Add(ln);
                    addedLines++;
                }
            }
            if (addedLines > 0)
                l.Info("Added {0} lines", addedLines);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            ff.Stop();
            base.OnClosing(e);
        }

        private void listView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fixGui.SelectedLine == null)
                return;
            Console.WriteLine("Selected=" + fixGui.SelectedLine.ToString());
        }

        private void FollowOrderContextMenu_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Follow=" + fixGui.SelectedLine.ToString());
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
            
            SetOrderFilter(fixGui.SelectedLine);
            view.Filter = UserFilter;
        }

        private void SetOrderFilter(Line l)
        {
            ff.SetOrderFilter(l);
        }

        private void ClearFollowContextMenu_Click(object sender, RoutedEventArgs e)
        {
            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(listView.ItemsSource);
            view.Filter =  null;
        }

        private bool UserFilter(object item)
        {
            Line  lt = (Line )item;
            return lt.CanSee;
        }
    }
}
