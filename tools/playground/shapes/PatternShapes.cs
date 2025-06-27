namespace shapes;

public interface IPattenShape
{
    IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
}

public static class PatternShapes
{
    public static RhombusShape Rhombus(int size) => new(size);

    public static void Print(this IPattenShape shape)
    {
        Console.WriteLine($"Rhombus Size: {shape.Positions.Count}");
        
        foreach (var row in shape.Positions)
        {
            foreach (var cell in row)
                Console.Write(cell ? "X" : " ");

            Console.WriteLine();
        }

        Console.WriteLine();
    }

    public class RhombusShape : IPattenShape
    {
        public RhombusShape(int size)
        {
            var isEven = size % 2 == 0;
            
            if (size % 2 == 0)
                size += 2;
            
            var positions = new bool[size][];
            var center = (size - 1) / 2.0;

            for (var i = 0; i < size; i++)
            {
                positions[i] = new bool[size];
                
                for (var j = 0; j < size; j++)
                {
                    var distance = Math.Abs(i - center) + Math.Abs(j - center);
                    positions[i][j] = distance <= center;
                }
            }
            
            if (isEven)
            {
                for (var i = 0; i < size; i++)
                {
                    var currentRow = positions[i];
                    var newRow = new bool[size - 2];

                    for (var j = 0; j < size - 2; j++)
                        newRow[j] = currentRow[j + 1];
                    
                    positions[i] = newRow;
                }
                
                var newPositions = new bool[size - 2][];
                
                for (var i = 0; i < size - 2; i++)
                    newPositions[i] = positions[i + 1];
                
                positions = newPositions;
            }

            Positions = positions;
        }

        public IReadOnlyList<IReadOnlyList<bool>> Positions { get; }
    }
}