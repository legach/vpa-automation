namespace vpa_automation;

public class ManifestCreator
{
    private const string OutputDirectory = "Generated Manifests";
    private const string Template = @"
    apiVersion: autoscaling.k8s.io/v1
    kind: VerticalPodAutoscaler
    metadata:
      name: vpa-{0}
      namespace: {1}
    spec:
      targetRef:
        apiVersion: ""apps/v1""
        kind:       Deployment
        name:       {0}
      updatePolicy:
        updateMode: ""Off""
    ";

    private readonly KubectlWrapper _kubectl;
    private readonly InteractionHelper _interactionHelper;


    public ManifestCreator(KubectlWrapper kubectl, InteractionHelper interactionHelper)
    {
        _kubectl = kubectl;
        _interactionHelper = interactionHelper;
    }

    public void Interact()
    {
        Directory.CreateDirectory(OutputDirectory);

        Console.WriteLine($"Select one of namespace.");
        var nsList = _kubectl.GetNamespaces();
        var nsIndex = _interactionHelper.SelectFromList(nsList);
        var ns = nsList[nsIndex];

        Console.WriteLine($"Select one of deployment.");
        var dpList = _kubectl.GetDeployments(ns);
        var dpIndex = _interactionHelper.SelectFromList(dpList);
        var deployment = dpList[dpIndex];

        var filePath = Path.Combine(OutputDirectory, $"vpa-{deployment}.yml");
        var content = string.Format(Template, deployment, ns);
        File.WriteAllText(filePath, content);

        Console.WriteLine($"You can open file: {Path.GetFullPath(filePath)}");
    }
}