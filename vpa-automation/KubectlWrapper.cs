using System.Diagnostics;
using System.Text;

namespace vpa_automation;

public class KubectlWrapper
{
    public string Run(string arguments)
    {
        var processInfo = new ProcessStartInfo("kubectl", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        var sb = new StringBuilder();
        var p = Process.Start(processInfo);
        p.OutputDataReceived += (sender, args) =>
        {
            sb.AppendLine(args.Data);
            if (args.Data != null && args.Data.Contains("couldn't get current server"))
            {
                p.Close();
            }
        };
        p.BeginOutputReadLine();
        p.WaitForExit();
        return sb.ToString();
    }

    public string[] GetNamespaces()
    {
        var allNsString = Run("get ns -o jsonpath='{.items[*].metadata.name}'");
        var nsList = allNsString.Replace("'", "").Trim().Split(' ');
        return nsList;
    }

    public string[] GetDeployments(string namespaceName)
    {
        var allDpsString = Run($"get deploy -n {namespaceName} -o jsonpath='{{.items[*].metadata.name}}'");
        var dpList = allDpsString.Replace("'", "").Trim().Split(' ');
        return dpList;
    }

    public string[] GetVpa(string namespaceName)
    {
        var allVpaString = Run($"get vpa -n {namespaceName} -o jsonpath='{{.items[*].metadata.name}}'");
        var vpaList = allVpaString.Replace("'", "").Trim().Split(' ');
        return vpaList;
    }
}