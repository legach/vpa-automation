using Newtonsoft.Json;

namespace vpa_automation;

public class RecommendationViewer
{
    private readonly KubectlWrapper _kubectl;
    private readonly InteractionHelper _interactionHelper;

    public RecommendationViewer(KubectlWrapper kubectl, InteractionHelper interactionHelper)
    {
        _kubectl = kubectl;
        _interactionHelper = interactionHelper;
    }

    public void Interact()
    {
        Console.WriteLine($"Select one of namespace.");
        var nsList = _kubectl.GetNamespaces();
        var nsIndex = _interactionHelper.SelectFromList(nsList);
        var ns = nsList[nsIndex];

        var getVpaTemplate = "get vpa {0} -n {1}";
        var outputFormat = " -o=jsonpath=\"{.spec.targetRef}{'|'}{.status.recommendation.containerRecommendations}\"";
        var targetRecommendation = new Dictionary<string, ContainerRecommendationDto[]>();
        var vpaList = _kubectl.GetVpa(ns);
        foreach (var vpa in vpaList)
        {
            var argument = string.Format(getVpaTemplate, vpa, ns) + outputFormat;
            var describeOutput = _kubectl.Run(argument);
            if (string.IsNullOrWhiteSpace(describeOutput) || !describeOutput.Contains('|'))
            {
                continue;
            }
            var splittedOutput = describeOutput.Split('|');
            var targetName = JsonConvert.DeserializeObject<TargetRefDto>(splittedOutput[0])?.Name;
            var recommedations = JsonConvert.DeserializeObject<ContainerRecommendationDto[]>(splittedOutput[1]);
            if (string.IsNullOrWhiteSpace(targetName) || recommedations == null)
            {
                Console.WriteLine($"Can't get recommendation for {vpa}.");
                continue;
            }
            targetRecommendation.Add(targetName, recommedations);
        }

        var containerRecommendations = new List<Container>();
        var getDpTemplate = "get deployment {0} -n {1}";
        var outputDpFormat = " -o=jsonpath=\"{range .spec.template.spec.containers[*]}{.name}{'|'}{.resources}{'\\n'}{end}\"";
        foreach (var pair in targetRecommendation)
        {
            var deploymentName = pair.Key;
            var argument = string.Format(getDpTemplate, deploymentName, ns) + outputDpFormat;
            var describeOutput = _kubectl.Run(argument);
            if (string.IsNullOrWhiteSpace(describeOutput) || !describeOutput.Contains('|'))
            {
                continue;
            }

            var outputByContainers = describeOutput.Split("\n");
            foreach (var outputByContainer in outputByContainers)
            {
                if (string.IsNullOrWhiteSpace(outputByContainer))
                {
                    continue;
                }
                var splittedOutput = outputByContainer.Trim().Split("|");
                var containerName = splittedOutput[0];
                var inUseResources = JsonConvert.DeserializeObject<ContainerResourceDto>(splittedOutput[1]);
                if (string.IsNullOrWhiteSpace(containerName) || inUseResources == null)
                {
                    Console.WriteLine($"Can't get container for {deploymentName}.");
                    continue;
                }

                var recommendation = pair.Value.FirstOrDefault(r => r.ContainerName == containerName) 
                                     ?? new ContainerRecommendationDto();

                var container = new Container()
                {
                    Name = containerName,
                    DeploymentName = deploymentName,
                    Limits = new Resources(inUseResources.Limits.Cpu, inUseResources.Limits.Memory),
                    Requests = new Resources(inUseResources.Requests.Cpu, inUseResources.Requests.Memory),
                    Target = new Resources(recommendation.Target.Cpu, recommendation.Target.Memory),
                    UncappedTarget = new Resources(recommendation.UncappedTarget.Cpu,
                        recommendation.UncappedTarget.Memory),
                    LowerBound = new Resources(recommendation.LowerBound.Cpu, recommendation.LowerBound.Memory),
                    UpperBound = new Resources(recommendation.UpperBound.Cpu, recommendation.UpperBound.Memory)
                };
                containerRecommendations.Add(container);
            }
        }

        OutputRecommendations(containerRecommendations);
    }

    private void OutputRecommendations(List<Container> containerRecommendations)
    {
        var template = "|{0,25}|{1,12}|{2,12}|{3,12}|{4,12}|{5,12}|{6,12}|";
        var header = string.Format(template, "", "CPU (req)", "MEM (req)", "CPU (lim)", "MEM (lim)", "CPU (r/t)", "MEM (r/t)");
        var line = new string('-', header.Length);
        Console.WriteLine(header);
        Console.WriteLine(line);
        foreach (var item in containerRecommendations)
        {
            Console.WriteLine(template, 
                Truncate(item.DeploymentName,25), 
                $"r: {item.Requests.Cpu}",
                $"r: {item.Requests.Memory}",
                $"l: {item.Limits.Cpu}",
                $"l: {item.Limits.Memory}",
                $"{CalculateDifference(item.Requests.CpuInMillicores, item.Target.CpuInMillicores)}%",
                $"{CalculateDifference(item.Requests.MemoryInBytes, item.Target.MemoryInBytes)}%"

                );
            Console.WriteLine(template,
                item.Name,
                $"t: {item.Target.Cpu}",
                $"t: {item.Target.Memory}",
                $"lb: {item.LowerBound.Cpu}",
                $"lb: {item.LowerBound.Memory}",
                "",
                ""
            );
            Console.WriteLine(template,
                "",
                $"ut: {item.UncappedTarget.Cpu}",
                $"ut: {item.UncappedTarget.Memory}",
                $"ub: {item.UpperBound.Cpu}",
                $"ub: {item.UpperBound.Memory}",
                $"",
                $""
            );
            Console.WriteLine(line);
        }
    }

    private string CalculateDifference(long request, long target)
    {
        return request == 0 ? "N/A" : $"{Math.Round(((1.0 * request) / target) * 100)}";
    }

    private string Truncate(string value, int maxChars)
    {
        return value.Length <= maxChars ? value : value.Substring(0, maxChars-2) + "..";
    }

    public record ResourcesDto(string Cpu = "N/A", string Memory = "N/A");
    public record TargetRefDto(string? Name );


    public class ContainerRecommendationDto
    {
        public string ContainerName { get; set; }
        public ResourcesDto LowerBound { get; set; } = new();
        public ResourcesDto Target { get; set; } = new();
        public ResourcesDto UncappedTarget { get; set; } = new();
        public ResourcesDto UpperBound { get; set; } = new();
    }

    public class ContainerResourceDto
    {
        public ResourcesDto Limits { get; set; } = new();
        public ResourcesDto Requests { get; set; } = new();
    }

    public class Resources
    {
        private readonly List<string> _unitLettersOrder = new List<string> {"K","M","G","T","P","E" };
        public Resources(string cpu, string memory)
        {
            CpuInMillicores = ConvertCpuToDigits(cpu);
            MemoryInBytes = ConvertMemoryToDigits(memory);
        }

        private long ConvertCpuToDigits(string value)
        {
            if (value.EndsWith('m') && long.TryParse(value.TrimEnd('m'), out var result))
            {
                return result;
            }

            if (float.TryParse(value, out var koef))
            {
                return (long)(koef * 1000);
            }

            return 0; //TODO: fix this
        }

        private long ConvertMemoryToDigits(string value)
        {
            var unitLetterCount = value.EndsWith('i') ? 2 : 1;
            var unit = value.EndsWith('i') ? 1024 : 1000;
            
            var digitValueString = value[..^unitLetterCount];
            var letters = value.Substring(value.Length-unitLetterCount,1);
            if (long.TryParse(digitValueString, out var digitValue))
            {
                long result = 0;
                var letterIndex = _unitLettersOrder.IndexOf(letters.ToUpper());
                result = (long)(digitValue * Math.Pow(unit, letterIndex + 1));
                return result;
            }

            return 0; //TODO: fix this
        }

        private int ValueLength(long value)
        {
            if (value == 0)
                return 1;
            return (int)Math.Floor(Math.Log10(value)) + 1;
        }

        public long CpuInMillicores { get; set; }
        public long MemoryInBytes { get; set; }

        public string Cpu => $"{CpuInMillicores}m";
        public string Memory
        {
            get
            {
                var valueLength = ValueLength(MemoryInBytes);
                var unitIndex = valueLength / 3;
                var powKoef = unitIndex > 6 ? 6 : unitIndex-(valueLength % 3 > 0 ? 0 : 1);
                var result = MemoryInBytes / Math.Pow(1024, powKoef);
                var unit = powKoef > 0 ? _unitLettersOrder[powKoef-1] + "i" : "";
                return $"{result:F1}{unit}";
            }
        }
    }

    public class Container
    {
        public string Name { get; set; }
        public string DeploymentName { get; set; }
        public Resources LowerBound { get; set; }
        public Resources Target { get; set; }
        public Resources UncappedTarget { get; set; }
        public Resources UpperBound { get; set; }
        public Resources Limits { get; set; }
        public Resources Requests { get; set; }
    }
}
