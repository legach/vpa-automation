namespace vpa_automation;

public class InteractionHelper
{
    public int SelectFromList(string[] list)
    {
        for (int i = 0; i < list.Length; i++)
        {
            Console.Write($"[{i + 1}] {list[i]}\t");
        }

        var inputIndex = 0;
        Console.Write($"\nSelect number(1..{list.Length}): ");
        var input = Console.ReadLine();
        while (!int.TryParse(input, out inputIndex) || (inputIndex > list.Length || inputIndex < 1))
        {
            Console.Write("Error: can't find such index. Try again: ");
            input = Console.ReadLine();
        }

        return inputIndex - 1;
    }
}