using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;

namespace SetIP
{
    public partial class MainWindow : Window
    {
        private string[] _preferredIps;
        private string _oldIp;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            SpNewIp.Visibility = Visibility.Collapsed;
            BtnSetToOld.IsEnabled = false;

            LblIpResult.Content = "";
            LblSubMaskResult.Content = "";
            LblGatewayResult.Content = "";
            LblDns1Result.Content = "";
            LblDns2Result.Content = "";

            BtnSet.IsEnabled = false;
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string file = Path.Combine(dir, "ips.ini");
            if (File.Exists(file))
            {
                _preferredIps = File.ReadAllLines(file);
            }

            GetNetAdapters();
        }

        private void BtnOpenNetworkConnections_OnClick(object sender, RoutedEventArgs e)
        {
            var startInfo = new ProcessStartInfo("ncpa.cpl");
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }

        public void GetNetAdapters()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                var addr = ni.GetIPProperties().GatewayAddresses.FirstOrDefault();
                if (addr != null)
                {
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    {
                        LbNic.Items.Add(ni.Description);
                    }
                }
            }

            LbNic.SelectedIndex = 0;
        }

        private void LbNic_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbNic.SelectedItem != null)
            {
                var nic = LbNic.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(nic))
                {
                    TxtIp.Text = "";
                    TxtSubMask.Text = "";
                    TxtGateway.Text = "";
                    TxtPrimaryDns.Text = "";
                    TxtBackupDns.Text = "";

                    GetNetworkConfig(nic);
                    BtnSet.IsEnabled = true;
                    if (_preferredIps != null)
                    {
                        LblNewIp.Content = _preferredIps[LbNic.SelectedIndex];

                        if (TxtIp.Text != (string)LblNewIp.Content)
                        {
                            SpNewIp.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            SpNewIp.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                else
                {
                    BtnSet.IsEnabled = false;
                }
            }
            else
            {
                BtnSet.IsEnabled = false;
            }
        }

        private void GetNetworkConfig(string nic)
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.Description != nic)
                {
                    continue;
                }

                IPInterfaceProperties ips = adapter.GetIPProperties();     //IP配置信息
                foreach (UnicastIPAddressInformation ip in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork && ip.PrefixLength == 24)
                    {
                        TxtIp.Text = ip.Address.ToString();//IP地址
                        TxtSubMask.Text = ip.IPv4Mask.ToString();//子网掩码
                    }
                }

                if (ips.GatewayAddresses.Count > 0)
                {
                    TxtGateway.Text = ips.GatewayAddresses[0].Address.ToString();//默认网关
                }

                int dnsCount = ips.DnsAddresses.Count;
                if (dnsCount > 0)
                {
                    try
                    {
                        TxtPrimaryDns.Text = ips.DnsAddresses[0].ToString(); //主DNS
                        TxtBackupDns.Text = ips.DnsAddresses[1].ToString(); //备用DNS地址
                    }
                    catch (Exception ex)
                    {
                        //throw ex;
                    }
                }
            }
        }

        private void BtnSet_OnClick(object sender, RoutedEventArgs e)
        {
            BtnSet.IsEnabled = false;
            LblIpResult.Content = "";
            LblSubMaskResult.Content = "";
            LblGatewayResult.Content = "";
            LblDns1Result.Content = "";
            LblDns2Result.Content = "";

            if (string.IsNullOrEmpty(TxtIp.Text) || string.IsNullOrEmpty(TxtSubMask.Text))
            {
                LblIpResult.Content = "不能为空";
                LblSubMaskResult.Content = "不能为空";

                return;
            }

            SetIpInfo(LbNic.SelectedItem.ToString(),
                new[] { TxtIp.Text },
                new[] { TxtSubMask.Text }, new[] { TxtGateway.Text },
                new[] { TxtPrimaryDns.Text, TxtBackupDns.Text });
            BtnSet.IsEnabled = true;
        }

        protected void SetIpInfo(string nic, string[] ip, string[] submask, string[] gateway, string[] dns)
        {
            var mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                if (!(bool)mo["IPEnabled"]) continue;
                if (mo["Caption"].ToString().Contains(nic))
                {
                    string str;
                    ManagementBaseObject inPar;
                    ManagementBaseObject outPar;
                    if (ip != null && submask != null)
                    {
                        inPar = mo.GetMethodParameters("EnableStatic");
                        inPar["IPAddress"] = ip;
                        inPar["SubnetMask"] = submask;
                        outPar = mo.InvokeMethod("EnableStatic", inPar, null);
                        str = outPar["returnvalue"].ToString();
                        if (str == "0" || str == "1")
                        {
                            LblIpResult.Content = $"修改成功,原IP {_oldIp}";
                            LblSubMaskResult.Content = "成功";
                        }
                        else
                        {
                            LblIpResult.Content = "失败";
                            LblSubMaskResult.Content = "失败";
                        }

                        //获取操作设置IP的返回值， 可根据返回值去确认IP是否设置成功。 0或1表示成功 
                        // 返回值说明网址： https://msdn.microsoft.com/en-us/library/aa393301(v=vs.85).aspx
                    }

                    if (gateway != null)
                    {
                        inPar = mo.GetMethodParameters("SetGateways");
                        inPar["DefaultIPGateway"] = gateway;
                        outPar = mo.InvokeMethod("SetGateways", inPar, null);
                        str = outPar["returnvalue"].ToString();
                        if (str == "0" || str == "1")
                        {
                            LblGatewayResult.Content = "成功";
                        }
                        else
                        {
                            LblGatewayResult.Content = "失败";
                        }
                    }

                    if (dns != null)
                    {
                        inPar = mo.GetMethodParameters("SetDNSServerSearchOrder");
                        inPar["DNSServerSearchOrder"] = dns;
                        outPar = mo.InvokeMethod("SetDNSServerSearchOrder", inPar, null);
                        str = outPar["returnvalue"].ToString();
                        if (str == "0" || str == "1")
                        {
                            LblDns1Result.Content = "成功";
                            LblDns2Result.Content = "成功";
                        }
                        else
                        {
                            LblDns1Result.Content = "失败";
                            LblDns2Result.Content = "失败";
                        }
                    }
                }
            }
        }

        private void BtnReplaceIp_OnClick(object sender, RoutedEventArgs e)
        {
            BtnSetToOld.IsEnabled = true;
            _oldIp = TxtIp.Text;
            TxtIp.Text = _preferredIps[LbNic.SelectedIndex];
        }

        private void BtnSetToOld_OnClick(object sender, RoutedEventArgs e)
        {
            TxtIp.Text = _oldIp;
        }
    }
}