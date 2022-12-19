using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using IDAnalyzer;

namespace IDAnalyzer_Demo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (picker.ShowDialog() != DialogResult.OK) return;
            textBox2.Text = picker.FileName;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (picker.ShowDialog() != DialogResult.OK) return;
            textBox3.Text = picker.FileName;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (picker.ShowDialog() != DialogResult.OK) return;
            textBox4.Text = picker.FileName;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var profile = comboBox1.Text;
            var front = textBox2.Text;
            var back = textBox3.Text;
            var face = textBox4.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if (profile == "")
            {
                MessageBox.Show("profile cannot empty.");
                return;
            }
            if (front == "")
            {
                MessageBox.Show("pick one document front.");
                return;
            }
            try
            {
                var p = new Profile(profile);
                var s = new Scanner(apiKey);
                s.throwApiException(true);
                s.setProfile(p);
                var jObj = s.scan(front, back, face);
                textBox1.Text = jObj.ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var profile = comboBox1.Text;
            var front = textBox2.Text;
            var back = textBox3.Text;
            if(apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if(profile == "")
            {
                MessageBox.Show("profile cannot empty.");
                return;
            }
            if(front == "")
            {
                MessageBox.Show("pick one document front.");
                return;
            }
            try
            {
                var s = new Scanner(apiKey);
                s.throwApiException(true);
                var jObj = s.quickScan(front, back, false);
                textBox1.Text = jObj.ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (picker.ShowDialog() != DialogResult.OK) return;
            textBox6.Text = picker.FileName;
        }

        private void button9_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (picker.ShowDialog() != DialogResult.OK) return;
            textBox7.Text = picker.FileName;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var face1 = textBox6.Text;
            var face2 = textBox7.Text;
            var p = comboBox2.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if (face1 == "")
            {
                MessageBox.Show("pick one face1.");
                return;
            }
            if (face2 == "")
            {
                MessageBox.Show("pick one face2.");
                return;
            }
            try
            {
                var profile = new Profile(p);
                var b = new Biometric(apiKey);
                b.throwApiException(true);
                b.setProfile(profile);
                var jObj = b.verifyFace(textBox6.Text, textBox7.Text);
                textBox1.Text = jObj.ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            var picker = new OpenFileDialog();
            picker.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";
            if (picker.ShowDialog() != DialogResult.OK) return;
            textBox8.Text = picker.FileName;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var face1 = textBox8.Text;
            var p = comboBox2.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if (face1 == "")
            {
                MessageBox.Show("pick one face1.");
                return;
            }
            try
            {
                var profile = new Profile(p);
                var b = new Biometric(apiKey);
                b.throwApiException(true);
                b.setProfile(profile);
                var jObj = b.verifyLiveness(face1);
                textBox1.Text = jObj.ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            try
            {
                var t = new Transaction(apiKey);
                t.throwApiException(true);
                textBox1.Text = t.listTransaction().ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var tid = textBox9.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if(tid == "")
            {
                MessageBox.Show("tid cannot empty.");
                return;
            }
            try
            {
                var t = new Transaction(apiKey);
                t.throwApiException(true);
                textBox1.Text = t.getTransaction(tid).ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button13_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var tid = textBox9.Text;
            var decision = comboBox3.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if (tid == "")
            {
                MessageBox.Show("tid cannot empty.");
                return;
            }
            if (decision == "")
            {
                MessageBox.Show("select one from decision.");
                return;
            }
            try
            {
                var t = new Transaction(apiKey);
                t.throwApiException(true);
                textBox1.Text = t.updateTransaction(tid, decision).ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button14_Click(object sender, EventArgs e)
        {
            var apiKey = textBox5.Text;
            var tid = textBox9.Text;
            if (apiKey == "")
            {
                MessageBox.Show("api key cannot empty.");
                return;
            }
            if (tid == "")
            {
                MessageBox.Show("tid cannot empty.");
                return;
            }
            try
            {
                var t = new Transaction(apiKey);
                t.throwApiException(true);
                textBox1.Text = t.deleteTransaction(tid).ToString();
            }
            catch (ApiError err)
            {
                MessageBox.Show($"{err.Msg} : {err.Code}");
                return;
            }
            catch (InvalidArgumentException err)
            {
                MessageBox.Show(err.Message);
                return;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
                return;
            }
        }

        private void button16_Click(object sender, EventArgs e)
        {
            try
            {
                var apiKey = textBox5.Text;
                if (apiKey == "")
                {
                    MessageBox.Show("api key cannot empty.");
                    return;
                }
                var d = new Docupass(apiKey);
                d.throwApiException(true);
                textBox1.Text = d.listDocupass().ToString();
            }
            catch (ApiError err)
            {
                Console.WriteLine(err.Msg, err.Code);
            }
            catch (InvalidArgumentException err)
            {
                Console.WriteLine(err.Message);
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }
        }

        private void button15_Click(object sender, EventArgs e)
        {
            try
            {
                var apiKey = textBox5.Text;
                var profileId = textBox10.Text;
                if (apiKey == "")
                {
                    MessageBox.Show("api key cannot empty.");
                    return;
                }
                if(profileId == "")
                {
                    MessageBox.Show("profile id cannot empty.");
                    return;
                }
                var d = new Docupass(apiKey);
                d.throwApiException(true);
                textBox1.Text = d.createDocupass(profileId).ToString();
            }
            catch (ApiError err)
            {
                Console.WriteLine(err.Msg, err.Code);
            }
            catch (InvalidArgumentException err)
            {
                Console.WriteLine(err.Message);
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }
        }

        private void button17_Click(object sender, EventArgs e)
        {
            try
            {
                var apiKey = textBox5.Text;
                var reference = textBox11.Text;
                if (apiKey == "")
                {
                    MessageBox.Show("api key cannot empty.");
                    return;
                }
                if (reference == "")
                {
                    MessageBox.Show("profile id cannot empty.");
                    return;
                }
                var d = new Docupass(apiKey);
                d.throwApiException(true);
                textBox1.Text = d.deleteDocupass(reference).ToString();
            }
            catch (ApiError err)
            {
                Console.WriteLine(err.Msg, err.Code);
            }
            catch (InvalidArgumentException err)
            {
                Console.WriteLine(err.Message);
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
            }
        }
    }
}
