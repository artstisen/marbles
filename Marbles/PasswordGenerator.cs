namespace Marbles;

internal class PasswordGenerator
{
    public PasswordGenerator()
    {
        InitializeGenerator();
    }

    // Set the password length here (should not be shorter than 8 characters to match the
    // criteria for a strong password)
    private  int NumberOfCharsInPassword { get; set; }
    private static Random _ran = new();
    private List<int> _previousRandomNumbers = new();
    private  List<string> _numbers = new();
    private  List<string> _uppercase = new();
    private  List<string> _lowercase = new();
    private  List<string> _symbols = new();

    private void InitializeGenerator()
    {
        // Create content

            // ASCII chars:
            // 0123456789
            // abcdefghijklmnopqrstuvwxyz
            // ABDCEFGHIJKLMNOPQRSTUVWXYZ
            string[] numbers = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
            _numbers = new List<string>(numbers);
            string[] lowercase = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o",
                "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z"};
            _lowercase = new List<string>(lowercase);
            string[] uppercase = { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O",
                "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"};
            _uppercase = new List<string>(uppercase);
            string[] symbols = { ".", "-", "!", "+" };
            _symbols = new List<string>(symbols);
            
    }


    private string _tempOutput = string.Empty;
    public string GeneratePassword()
    {
        // Set password length
        byte passwordLength = 12;
        InitializeGenerator();
        CreateCombinedCharacterString();
        _previousRandomNumbers.Clear();
        NumberOfCharsInPassword = passwordLength - 1; // Zero indexed
        _tempOutput = "";

        do
        {
            _tempOutput = Generate();
        }
        while (!DoesStringContainOneOfEachCharClass(_tempOutput));

        _tempSymbol1 = "";
        _tempSymbol2 = "";

        // Induce symbols
        _tempOutput = _tempOutput.Insert(_ran.Next(1, 5), GetRandomSymbol());
        _tempOutput = _tempOutput.Insert(_ran.Next(7, 11), GetRandomSymbol());

        return _tempOutput;
    }

    private string _tempSymbol1 = string.Empty;
    private string _tempSymbol2 = string.Empty;
    private string GetRandomSymbol()
    {
        do
        {
            _tempSymbol1 = _symbols[_ran.Next(0, _symbols.Count)];
        } while (_tempSymbol1 == _tempSymbol2);

        _tempSymbol2 = _tempSymbol1;
        return _tempSymbol2;
    }

    private string _tempGenerate = string.Empty;
    private string Generate()
    {
        _tempGenerate = "";
        for (int i = 0; i <= NumberOfCharsInPassword; i++)
        {
            _tempGenerate += GetChars(GetRandomNumber());
        }

        return _tempGenerate;
    }

    private int _tempRandomNumber;
    private int GetRandomNumber()
    {
        // Generate test number. If already in list, generate a new one
        if (_previousRandomNumbers.Count > 0)
        {
            do
            {
                _tempRandomNumber = _ran.Next(0, _strChars.Length);
            }
            while (DoesRandomNumberExist(_tempRandomNumber));
        }
        else
        {
            _tempRandomNumber = _ran.Next(0, _strChars.Length);
        }

        _previousRandomNumbers.Add(_tempRandomNumber);
        return _tempRandomNumber;
    }

    private bool DoesRandomNumberExist(int number)
    {
        return _previousRandomNumbers.Any(i => i == number);
    }

    private int _validationPoints;
    private bool DoesStringContainOneOfEachCharClass(string generatedPassword)
    {
        _validationPoints = 0;

        if (_numbers.Any(generatedPassword.Contains))
        {
            _validationPoints += 1;
        }
        if (_lowercase.Any(generatedPassword.Contains))
        {
            _validationPoints += 1;
        }
        if (_uppercase.Any(generatedPassword.Contains))
        {
            _validationPoints += 1;
        }

        return _validationPoints >= 3;
    }

    private string _strChars = string.Empty;
    private void CreateCombinedCharacterString()
    {
        _strChars = "";
        foreach (var s in _numbers)
        {
            _strChars += s;
        }
        foreach (var s in _lowercase)
        {
            _strChars += s;
        }
        foreach (var s in _uppercase)
        {
            _strChars += s;
        }
    }

    private char GetChars(int tmp)
    {
        return _strChars[tmp];
    }
}