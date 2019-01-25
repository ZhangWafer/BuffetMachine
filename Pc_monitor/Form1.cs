using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Speech.Recognition;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Newtonsoft.Json;
using System.Speech.Synthesis;
using Microsoft.SqlServer.Types;
using XinYu.Framework.Library.Implement;
using XinYu.Framework.Library.Implement.Security;


namespace Pc_monitor
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            //设置全屏
            //    this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            //   this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
        }


        private DataTable PcTable;
        private DataTable WorkerTable;
        private DataTable All_OrderDetail;
        private DataTable All_OrderTable;

        private void Form1_Load(object sender, EventArgs e)
        {
            button1.Enabled = false;
            //启动定时器
            timer1.Enabled = true;
            timer1.Start();
            //显示第二显示屏画面
            //  backForm bkForm = new backForm();
            // bkForm.Show();
            //读取用户表格---只在开机读取一次
            try
            {
                PcTable = SqlHelper.ExecuteDataTable("select * from Cater.PCStaff");
                WorkerTable = SqlHelper.ExecuteDataTable("select * from Cater.WorkerStaff");

                All_OrderDetail = SqlHelper.ExecuteDataTable("select * from Cater.CookbookSetInDateDetail");
                All_OrderTable = SqlHelper.ExecuteDataTable("select * from Cater.CookbookSetInDate");
            }
            catch (Exception exception)
            {

                MessageBox.Show("数据库连接失败!" + exception.Message);
            }

        }


        private int timer_count_10s = 9;
        //全局变量，存储当前二维码的
        private string personId = null;
        private string staffEnum = null;
        //CookbookSetInDate的表格
        DataTable dt2 = null;
        //插入一条记录
        private void InsertRecoed(string personEnum, string personId, string staffCanteen, string OrderId,
            string recordTime)
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.localsqlConn);
            conn.Open();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO [dbo].[TempRecord]([staffEnum],[staffId],[staffCanteen],[OrderId],[time],[upDateBool])VALUES('" +
                personEnum + "','" + personId + "','" + staffCanteen + "','" + OrderId + "','" + recordTime +
                "','false')";
            cmd.ExecuteNonQuery();
            conn.Close();
        }

        //查询是否重复刷卡
        private bool OverPay(string personId, string OrderId, string staffEnum)
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.localsqlConn);
            conn.Open();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "select COUNT(1) from dbo.TempRecord where staffId='" + personId + "' and OrderId='" +
                              OrderId + "' and staffEnum='" + staffEnum + "'";
            object count = cmd.ExecuteScalar();
            conn.Close();
            if (count.ToString() == "1")
            {
                return true;
            }
            return false;
        }

        //打菜号暂时变量
        public static int TempOrderId = 0;
        public static bool TakeOrderBool = true;
        public static bool AllowTakeOrderBool = true;
        int whole_catlocation = Properties.Settings.Default.catlocation;
        //语音
        SpeechRecognitionEngine recEngine = new SpeechRecognitionEngine();
        SpeechSynthesizer speech = new SpeechSynthesizer();

        public void SpeechVideo_Read(int rate, int volume, string speektext) //读
        {
            speech.Rate = rate;
            speech.Volume = volume;
            speech.SpeakAsync(speektext);
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            //1秒跑一次的程序1

            //1秒跑一次的程序2
            if (richTextBox1.Text.Contains("\n"))
            {
                try
                {

                    //解析扫码数据，拿取关键信息
                    string jsonText = richTextBox1.Text;
                    //二维码解密
                    jsonText = Encrypt.Decode(jsonText);
                    //json格式化
                    JavaScriptObject jsonObj = JavaScriptConvert.DeserializeObject<JavaScriptObject>(jsonText);
                    personId = jsonObj["Id"].ToString();
                    staffEnum = jsonObj["staffEnum"].ToString();
                    //检查是否存在这个人
                    DataRow[] selectedResult = PcTable.Select("Id=" + personId);
                    DataRow[] selectedResult_worker = WorkerTable.Select("Id=" + personId);
                    if (selectedResult.Length == 0 && selectedResult_worker.Length == 0)
                    {
                        richTextBox1.Text = "";
                        label2.Font = new Font("宋体粗体", 50);
                        label2.ForeColor = Color.Red;
                        label2.Text = "查无此人";
                        return;
                    }
                    //检查是否重复刷卡
                    if (OverPay(personId, TempOrderId.ToString(), staffEnum))
                    {
                        richTextBox1.Text = "";
                        label2.Text = "重复扫码！";
                        SpeechVideo_Read(0, 100, "重复扫码！");
                        return;
                    }
                    //警员接口拿照片

                    string picUrl = "http://" + Properties.Settings.Default.header_url +
                                    "/Interface/Icon/GetStaffIconByIpAddr.ashx?id=" + personId + "&staffType=" +
                                    staffEnum.ToLower()+"&addr="+Properties.Settings.Default.header_url;
                    try
                    {
                        string picResponse = GetFunction(picUrl);//照片url回复
                        //json格式化
                        JavaScriptObject jsonResponse = JavaScriptConvert.DeserializeObject<JavaScriptObject>(picResponse);
                        string responPicUrl = jsonResponse["icon"].ToString();

                        pictureBox1.Image = new Bitmap(new WebClient().OpenRead(responPicUrl));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        label2.Text = "拿取照片错误！";
                    }


                    //显示扫码成功！大字体
                    richTextBox1.Text = "";
                    label2.Font = new Font("宋体粗体", 50);
                    label2.ForeColor = Color.GreenYellow;
                    label2.Text = "扫码成功！";
                    SpeechVideo_Read(0, 100, "扫码成功！");

                    //扫码成功写入xml文件
                    //AppendXml(staffEnum, personId, whole_catlocation.ToString(), TempOrderId.ToString(),
                    //    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    //扫码成功写入数据库
                    InsertRecoed(staffEnum, personId, whole_catlocation.ToString(), TempOrderId.ToString(),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));


                    AllowTakeOrderBool = false;
                    TakeOrderBool = false;
                }
                catch (Exception EX)
                {

                    richTextBox1.Text = "";
                    label2.Text = "请出示正确的二维码";
                    SpeechVideo_Read(0, 100, "扫码错误！");
                }
                //写入文本，写入记录

            }

            //20秒跑一次程序
            if (timer_count_10s >= 10)
            {
                if (label2.Text == "扫码成功！" || label2.Text == "重复扫码！")
                {
                    label2.Text = Record_RecentOrder;
                }
                timer_count_10s = 0;
            }
            else
            {
                timer_count_10s++;
            }
        }



        private int selectedNum = 0;



        private void button1_Click(object sender, EventArgs e)
        {
            TakeOrderBool = true;
            label2.Text = "请出示二维码";
            // SpeechVideo_Read(-5, 100, "请出示饿维马！");
            label2.Font = new Font("宋体粗体", 50);
            label2.ForeColor = Color.Red;
            richTextBox1.Focus();
        }

        private void AppendXml(string Type, string Id, string CafeteriaId, string CookbookSetInDateId, string Datatime)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(@"d:\User.xml");
            XmlNode root = xmlDoc.SelectSingleNode("Root"); //查找<bookstore>
            XmlElement xe1 = xmlDoc.CreateElement("User"); //创建一个<book>节点
            xe1.SetAttribute("Type", Type); //设置该节点的genre属性
            xe1.SetAttribute("Id", Id); //设置该节点的ISBN属性

            XmlElement xesub1 = xmlDoc.CreateElement("CafeteriaId"); //添加一个名字为title的子节点
            xesub1.InnerText = CafeteriaId; //设置文本NM
            xe1.AppendChild(xesub1); //把title添加到<book>节点中

            XmlElement xesub2 = xmlDoc.CreateElement("CookbookSetInDateId");
            xesub2.InnerText = CookbookSetInDateId;
            xe1.AppendChild(xesub2);

            XmlElement xesub3 = xmlDoc.CreateElement("Datatime");
            xesub3.InnerText = Datatime;
            xe1.AppendChild(xesub3);

            root.AppendChild(xe1); //把book添加到<bookstore>根节点中
            xmlDoc.Save(@"d:\User.xml");
        }

        private string Record_RecentOrder = "";

        private void button2_Click(object sender, EventArgs e)
        {
            //更新数据表
            PcTable = SqlHelper.ExecuteDataTable("select * from Cater.PCStaff");
            WorkerTable = SqlHelper.ExecuteDataTable("select * from Cater.WorkerStaff");
            All_OrderDetail = SqlHelper.ExecuteDataTable("select * from Cater.CookbookSetInDateDetail");
            All_OrderTable = SqlHelper.ExecuteDataTable("select * from Cater.CookbookSetInDate");
            //分割线·············分割线//
            int catlocation = Properties.Settings.Default.catlocation;
            DateTime currentTime = new DateTime();
            currentTime = DateTime.Now;
            string st1 = Properties.Settings.Default.b1; //早餐前

            string st2 = Properties.Settings.Default.b2; //早餐后

            string st3 = Properties.Settings.Default.l1; //午餐前

            string st4 = Properties.Settings.Default.l2; //午餐后

            string st5 = Properties.Settings.Default.d1; //午餐后

            string st6 = Properties.Settings.Default.d2; //午餐后

            DateTime b1DateTime = Convert.ToDateTime(st1);
            DateTime b2DateTime = Convert.ToDateTime(st2);
            DateTime l1DateTime = Convert.ToDateTime(st3);
            DateTime l2DateTime = Convert.ToDateTime(st4);
            DateTime d1DateTime = Convert.ToDateTime(st5);
            DateTime d2DateTime = Convert.ToDateTime(st6);


            string currentCat = "";
            string showString = "";
            if (DateTime.Compare(currentTime, b1DateTime) > 0 && DateTime.Compare(currentTime, b2DateTime) < 0)
            {
                currentCat = "Breakfast";
                showString = "早餐";
            }
            else if (DateTime.Compare(currentTime, l1DateTime) > 0 && DateTime.Compare(currentTime, l2DateTime) < 0)
            {
                currentCat = "Lunch";
                showString = "午餐";
            }
            else if (DateTime.Compare(currentTime, d1DateTime) > 0 && DateTime.Compare(currentTime, d2DateTime) < 0)
            {
                currentCat = "Supper";
                showString = "晚餐";
            }
            else
            {
                MessageBox.Show("当前不在用餐时段");
                return;
            }


            if (dt2 != null)
            {
                dt2.Clear();
            }

            //拿出今天的日期当前时段的餐次及其价格
            string todayDate = DateTime.Now.ToString("yyyy-MM-dd");
            try
            {
                dt2 =
                    SqlHelper.ExecuteDataTable("select * from  Cater.CookbookSetInDate where CafeteriaId=" + catlocation +
                                               " and CookbookEnum='" + currentCat + "' and ChooseDate='" + todayDate +
                                               "'");
            }
            catch (Exception)
            {
                MessageBox.Show("数据库连接错误");
                return;
            }
            try
            {
                TempOrderId = Int32.Parse(dt2.Rows[0][0].ToString());
            }
            catch (Exception)
            {

                MessageBox.Show("查无排餐");
                return;
            }
            label4.Text = showString ;
            Record_RecentOrder = label4.Text;
            button1.Enabled = true;
        }

        List<string> OrderFoodList = new List<string>();

        public void button_MouseClick(object sender, EventArgs e)
        {
            //拿取数据
            Button button = (Button) sender;
            var NameArray = button.Name.Split('*');
            //调整label2字体
            label2.Font = new Font("黑体", 22);
            label2.ForeColor = Color.Red;
            //添加显示菜品
            if (label2.Text == "")
            {
                label2.Text = "当前选择餐次：" + button.Text + " ";
            }
            else
            {
                label2.Text += button.Text + " ";
            }
            //添加菜品进数组
            OrderFoodList.Add(NameArray[1]);

        }

        //将本地的记录通过调用接口提交服务器
        private void button3_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            string header_url = Properties.Settings.Default.header_url;
            try
            {
                DataTable recorDataTable = GetTempRecord("Police");

                //提交字符串url   警员
                for (int i = 0; i < recorDataTable.Rows.Count; i++)
                {
                    string get_url = "http://" + header_url + "/Interface/Synchronize/PCCommonSynchronize.ashx?pcId=" + recorDataTable.Rows[i][2] + "&cafeteriId=" + recorDataTable.Rows[i][3] + "&cookbookSetInDateId=" + recorDataTable.Rows[i][4];
                    GetFunction(get_url);
                }
                DataTable recorDataTable2 = GetTempRecord("Worker");
                //提交字符串url   职工
                for (int i = 0; i < recorDataTable2.Rows.Count; i++)
                {
                    string get_url = "http://" + header_url + "/Interface/Synchronize/WorkerCommonSynchronize.ashx?workerId=" + recorDataTable2.Rows[i][2] + "&cafeteriId=" + recorDataTable2.Rows[i][3] + "&cookbookSetInDateId=" + recorDataTable2.Rows[i][4];
                    GetFunction(get_url);
                }

                DataTable recorDataTable3 = GetTempRecord("Family");
                //提交字符串url   家属
                for (int i = 0; i < recorDataTable3.Rows.Count; i++)
                {
                    string get_url = "http://" + header_url + "/Interface/Synchronize/FamilyCommonSynchronize.ashx?familyId=" + recorDataTable3.Rows[i][2] + "&cafeteriId=" + recorDataTable3.Rows[i][3] + "&cookbookSetInDateId=" + recorDataTable3.Rows[i][4];
                    GetFunction(get_url);
                }


                this.Enabled = true;
                MessageBox.Show("同步完成！");
                ChangeUpdateTable();
            }
            catch (Exception essException)
            {
                this.Enabled = true;
                MessageBox.Show(essException.Message);
            }

        }

        private void ChangeUpdateTable()
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.localsqlConn);
            conn.Open();
            SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE [LocalRecord].[dbo].[TempRecord] SET [upDateBool] = 'true' WHERE [upDateBool]='false'";
            cmd.ExecuteNonQuery();
            conn.Close();
        }

        //获取record表
        private DataTable GetTempRecord(string Pc_Worker_Family)
        {
            SqlConnection conn = new SqlConnection(Properties.Settings.Default.localsqlConn);
            conn.Open();
            SqlCommand sqlCommand = new SqlCommand("select * from dbo.TempRecord where staffEnum='" + Pc_Worker_Family + "' and upDateBool='0'", conn);
            SqlDataAdapter sqlDataAdapter=new SqlDataAdapter(sqlCommand);
            DataTable tempDatetable=new DataTable();
            sqlDataAdapter.Fill(tempDatetable);
            conn.Close();
            return tempDatetable;
           
        }

        //get方法
        private string GetFunction(string url)
        {
            System.Net.HttpWebRequest request;
            // 创建一个HTTP请求  
            request = (System.Net.HttpWebRequest)WebRequest.Create(url);
            //设置超时5s
            request.Timeout = 5000;
            //request.Method="get";  
            System.Net.HttpWebResponse response;
            response = (System.Net.HttpWebResponse)request.GetResponse();
            System.IO.StreamReader myreader = new System.IO.StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string responseText = myreader.ReadToEnd();
            myreader.Close();
            return responseText;
        }

        private void panel1_DoubleClick(object sender, EventArgs e)
        {
            if( MessageBox.Show( "确定关机吗？", "提示", MessageBoxButtons.YesNo ) == DialogResult.Yes )
            {
                //关机代码
                System.Diagnostics.Process bootProcess = new System.Diagnostics.Process();
                bootProcess.StartInfo.FileName = "shutdown";
                bootProcess.StartInfo.Arguments = "/s";
                bootProcess.Start();
            }
        }



    }
}


