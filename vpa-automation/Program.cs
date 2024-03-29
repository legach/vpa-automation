using vpa_automation;

var interaction = new InteractionHelper();
var kubectl = new KubectlWrapper();
var manifestCreator = new ManifestCreator(kubectl, interaction);
var recommendationViewer = new RecommendationViewer(kubectl, interaction);


var commandList = new List<string>()
{
    "Create manifests",
    "Show recommendation",
    "Exit"
};

while (true)
{
    var commandIndex = interaction.SelectFromList(commandList.ToArray());
    switch (commandIndex)
    {
        case 0:
            manifestCreator.Interact();
            break;
        case 1:
            recommendationViewer.Interact(); 
            break;
        case 2:
            Console.WriteLine("Thank you for your work!");
            return;
        default:
            Console.WriteLine("Couldn't find. Try again.");
            break;
    }
}