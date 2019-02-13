﻿using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using Lockall_Windows.BrowserPlugin;
using Lockall_Windows.Forms;
using Lockall_Windows.Messages;
using Lockall_Windows.Messages.Pairing;
using Lockall_Windows.Messages.Password;
using Lockall_Windows.WinUtils;
using Newtonsoft.Json;
using WebSocketSharp.Server;

namespace Lockall_Windows
{
    public partial class FirstComponentForm : Form
    {
        private readonly TextBox[] _firstComponents;
        private readonly KeyboardHook _passCreate, _passLoad, _otpLoad;

        protected override bool ShowWithoutActivation => true;

        private readonly WebSocketServer _wsServer;

        public FirstComponentForm()
        {
            InitializeComponent();

            try
            {
                _wsServer = new WebSocketServer(IPAddress.Loopback, 42587);
            }
            catch (Exception ex)
            {
                // no plugin support
            }

            if (_wsServer == null)
            {
                MessageBox.Show("Cannot bind to 42587", "Browser plugin cannot be used", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                _wsServer.AddWebSocketService<ChromeComm>("/Lockall");
                _wsServer.Start();
            }

            _passCreate = new KeyboardHook();
            _passCreate.RegisterHotKey(WinUtils.ModifierKeys.Alt | WinUtils.ModifierKeys.Control, Keys.N);
            _passCreate.KeyPressed += _passCreate_KeyPressed;

            _passLoad = new KeyboardHook();
            _passLoad.RegisterHotKey(WinUtils.ModifierKeys.Alt | WinUtils.ModifierKeys.Control, Keys.Insert);
            _passLoad.KeyPressed += _passLoad_KeyPressed;

            _otpLoad = new KeyboardHook();
            _otpLoad.RegisterHotKey(WinUtils.ModifierKeys.Alt | WinUtils.ModifierKeys.Control, Keys.O);
            _otpLoad.KeyPressed += _otpLoad_KeyPressed;

            trayIcon.Icon = Icon;
            trayIcon.Visible = true;
            var contextMenu = new ContextMenu();
            contextMenu.MenuItems.AddRange(new [] {new MenuItem(), new MenuItem(), new MenuItem(), new MenuItem()});
            contextMenu.MenuItems[3].Text = "Exit";
            contextMenu.MenuItems[3].Click += (o, s) => { Application.ExitThread(); };
            contextMenu.MenuItems[2].Text = "Settings";
            contextMenu.MenuItems[2].Click += (o, s) => { new SettingsForm().Show(this); };
            contextMenu.MenuItems[1].Text = "Pair a new device";
            contextMenu.MenuItems[1].Click += PairingButton_Click;
            contextMenu.MenuItems[0].Text = "Edit keybase";
            contextMenu.MenuItems[0].Click += (o, s) =>
            {
                Show();
            };
            trayIcon.ContextMenu = contextMenu;

            _firstComponents = new[] { secw1Text, secw2Text, secw3Text, secw4Text, secw5Text, secw6Text };
            string[] components;
            if (File.Exists(ComponentsManager.FirstComponentFilename))
                components = File.ReadAllText(ComponentsManager.FirstComponentFilename).Split();
            else
                components = new string[_firstComponents.Length];
            for (int i = 0; i < _firstComponents.Length; ++i)
            {
                _firstComponents[i].Text = components[i];
                _firstComponents[i].KeyDown += FirstCompElementKeyDown;
                _firstComponents[i].LostFocus += FirstComponentLostFocus;
            }
        }

        private void PairingButton_Click(object sender, EventArgs e)
        {
            var name = Environment.MachineName + "/" + Environment.UserName;

            var pair = new QrDisplayerWindow("PAIR");
            pair.ShowQrForAJsonResult<MessageWithName>("PAIRING",
                JsonConvert.SerializeObject(
                    new MessageWithName(name)), true).ContinueWith(result =>
            {
                if (name == result.Result.name)
                {
                    MessageBox.Show("Pairing complete");
                }
            });
            pair.Show();
        }

        private void FirstComponentLostFocus(object sender, EventArgs e)
        {
            string current = "";
            for (int i = 0; i < _firstComponents.Length; ++i)
                current += _firstComponents[i].Text + " ";
            File.WriteAllText(ComponentsManager.FirstComponentFilename, current);
        }

        private void _otpLoad_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            var winHeader = TitleGetter.GetActiveWindowTitle();

            if (winHeader.EndsWith("Google Chrome") && ChromeComm.ChromeConnection != null)
            {
                ChromeComm.ChromeConnection.ShowQrInBrowserForAJsonResult<MessageWithPassword>("OTP",
                    JsonConvert.SerializeObject(
                        new MessageWithResourceid(""))).ContinueWith(result =>
                {
                    SendKeys.SendWait(result.Result.password);
                });
                return;
            }

            var load = new QrDisplayerWindow("OTP");
            load.ShowQrForAJsonResult<MessageWithPassword>("OTP", "{}").ContinueWith(result =>
            {
                SendKeys.SendWait(result.Result.password);
            });
            load.Show();
        }

        private void _passLoad_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            var winHeader = TitleGetter.GetActiveWindowTitle();

            if (winHeader.EndsWith("Google Chrome") && ChromeComm.ChromeConnection != null)
            {
                ChromeComm.ChromeConnection.ShowQrInBrowserForAJsonResult<MessageWithPassword>("PULL",
                    JsonConvert.SerializeObject(
                        new MessageWithResourceid(""))).ContinueWith(result =>
                {
                    SendKeys.SendWait(result.Result.password);
                });
                return;
            }

            var load = new QrDisplayerWindow("PULL");
            load.ShowQrForAJsonResult<MessageWithPassword>("PULL",
                JsonConvert.SerializeObject(
                    new MessageWithResourceid(winHeader))).ContinueWith(result =>
            {
                SendKeys.SendWait(result.Result.password);
            });
            load.Show();
        }

        private void _passCreate_KeyPressed(object sender, KeyPressedEventArgs e)
        {
            var winHeader = TitleGetter.GetActiveWindowTitle();

            var passAsker = new PasswordGetterForm(winHeader);
            var res = passAsker.ShowDialog();
            if (res == DialogResult.OK)
            {
                if (winHeader.EndsWith("Google Chrome") && ChromeComm.ChromeConnection != null)
                {
                    ChromeComm.ChromeConnection.ShowQrInBrowserForAJsonResult<MessageStatus>("STORE",
                        JsonConvert.SerializeObject(
                            new MessageWithPassword("", passAsker.PasswordResult))).ContinueWith(result =>
                    {
                    });
                    return;
                }
                var create = new QrDisplayerWindow("STORE");
                create.ShowQrForAJsonResult<MessageStatus>("STORE",
                    JsonConvert.SerializeObject(
                        new MessageWithPassword(winHeader, passAsker.PasswordResult))).ContinueWith(result =>
                        {
                        });
                create.Show();
            }
        }

        private void FirstComponentForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void FirstComponentForm_Shown(object sender, EventArgs e)
        {
            Hide();
        }

        private void FirstCompElementKeyDown(object sender, KeyEventArgs e)
        {
            int ix = 0;
            for (int i = 0; i < _firstComponents.Length; ++i)
                if (sender == _firstComponents[i])
                {
                    ix = i;
                    break;
                }
            ix = (ix + 1) % _firstComponents.Length;
            if (e.KeyCode == Keys.Space)
            {
                _firstComponents[ix].Focus();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }
    }
}
