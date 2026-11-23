using System.Windows;

namespace CuoHandshakePatcher;

public partial class DisclaimerWindow : Window
{
    public const string DisclaimerText =
        "This tool is provided for personal, educational, and interoperability testing purposes only.\n\n" +
        "By continuing, you acknowledge that modifying client software, network packet handling, or " +
        "challenge/response behavior is undertaken solely at your own discretion and risk. You are " +
        "responsible for ensuring that any use of this tool complies with applicable laws, server rules, " +
        "terms of service, and the policies of any game or shard you connect to.\n\n" +
        "This software is not affiliated with, endorsed by, or supported by the developers or maintainers " +
        "of Razor Enhanced, NeoUO, ClassicUO, or any Ultima Online shard or service. Those parties " +
        "assume no responsibility or liability for modifications made with this tool, including any " +
        "loss of data, account sanctions, software malfunction, security issues, or other damages that " +
        "may result from its use.\n\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING " +
        "BUT NOT LIMITED TO WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND " +
        "NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, " +
        "DAMAGES, OR OTHER LIABILITY ARISING FROM USE OF THIS SOFTWARE.\n\n" +
        "If you do not agree, select Decline and do not use this application.";

    public bool Accepted { get; private set; }

    public DisclaimerWindow()
    {
        InitializeComponent();
        DisclaimerBody.Text = DisclaimerText;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        Accepted = true;
        DialogResult = true;
        Close();
    }

    private void Decline_Click(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        DialogResult = false;
        Close();
    }
}
