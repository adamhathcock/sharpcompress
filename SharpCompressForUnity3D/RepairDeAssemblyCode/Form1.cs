using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace RepairDeAssemblyCode {
    public partial class Form1 : Form {
        public Form1() {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e) {
            string path = this.textBox1.Text.Trim();
            //字段自动修正
            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo[] csfiles = di.GetFiles("*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < csfiles.Length; i++) {
                RepairCsFieldByDeAssembly(csfiles[i]);
            }
            MessageBox.Show("修复<*>k__BackingField异常字段完成");
        }

        private void RepairCsFieldByDeAssembly(FileInfo csfile) {
            Application.DoEvents();
            StringBuilder sb = new StringBuilder();
            bool change = false;
            using (StreamReader sr = new StreamReader(csfile.FullName, Encoding.Default)) {
                string line = "";
                //String newLine = "";
                int fieldIndex = -1;
                while (sr.Peek() > 0) {
                    line = sr.ReadLine();
                    fieldIndex = line.IndexOf(">k__BackingField");
                    if (fieldIndex != -1) {
                        //查找<*>k__BackingField
                        int start_zuojian = -1;
                        for (int i = fieldIndex - 1; i >= 0; i--) {
                            if (line[i] == '<') {
                                start_zuojian = i;
                                break;
                            }
                        }
                        if (start_zuojian < 0) {
                            MessageBox.Show(csfile.FullName + "not find match <");
                            break;
                        }
                        unsafe {
                            fixed (char* sf = line) {
                                sf[start_zuojian] = '_';
                                sf[fieldIndex] = '_';
                            }
                        }
                        sb.AppendLine(line);
                        change = true;
                    }
                    else {
                        sb.AppendLine(line);
                    }
                }
            }
            if (change) {
                //修改
                using (StreamWriter sr = new StreamWriter(csfile.FullName, false, Encoding.Default)) {
                    sr.Write(sb.ToString());
                }
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            string path = this.textBox1.Text.Trim();
            //字段自动修正
            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo[] csfiles = di.GetFiles("*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < csfiles.Length; i++) {
                RepairCsJianKuoHaoByDeAssembly(csfiles[i]);
            }
            MessageBox.Show("修复<>异常字符完成");
        }
        private void RepairCsJianKuoHaoByDeAssembly(FileInfo csfile) {
            Application.DoEvents();
            StringBuilder sb = new StringBuilder();
            bool change = false;
            using (StreamReader sr = new StreamReader(csfile.FullName, Encoding.Default)) {
                string line = "";
                //String newLine = "";
                int fieldIndex = -1;
                while (sr.Peek() > 0) {
                    line = sr.ReadLine();
                    fieldIndex = line.IndexOf("<>");
                    if (fieldIndex != -1) {                       
                        unsafe {
                            fixed (char* sf = line) {
                                sf[fieldIndex] = '_';
                                sf[fieldIndex+1] = '_';
                            }
                        }
                        sb.AppendLine(line);
                        change = true;
                    }
                    else {
                        sb.AppendLine(line);
                    }
                }
            }
            if (change) {
                //修改
                using (StreamWriter sr = new StreamWriter(csfile.FullName, false, Encoding.Default)) {
                    sr.Write(sb.ToString());
                }
            }
        }
    }
}
