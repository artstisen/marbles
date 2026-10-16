using System.Security.Cryptography;
using System.Text;

namespace Marbles.Tests;

// The application keeps its state in static classes, so the tests must not run in parallel
[CollectionDefinition("Static state", DisableParallelization = true)]
public class StaticStateCollection
{
}

[Collection("Static state")]
public class EncryptorTests
{
    private const string Password = "correct-horse-battery";
    private const string Document = FileIo.XmlDoctype + "<entries><entry>æøå 日本語 ]]> \n line 2</entry></entries>";

    private static byte[] Encrypt(string password, string document)
    {
        Encryptor.SetKey(password);
        var encrypted = Encryptor.EncryptData(document);
        Assert.NotNull(encrypted);
        return encrypted!;
    }

    [Fact]
    public void RoundTrip_ReturnsOriginalDocument()
    {
        var encrypted = Encrypt(Password, Document);
        Encryptor.SetKey(Password); // As when logging in again
        Assert.Equal(Document, Encryptor.DecryptData(encrypted));
        Assert.False(Encryptor.LegacyFormatDetected);
    }

    [Fact]
    public void EncryptedFile_HasHeaderAndDoesNotContainPlainText()
    {
        var encrypted = Encrypt(Password, Document);
        Assert.Equal("MBLS", Encoding.ASCII.GetString(encrypted, 0, 4));
        Assert.Equal(2, encrypted[4]);
        Assert.DoesNotContain("entries", Encoding.UTF8.GetString(encrypted));
    }

    [Fact]
    public void WrongPassword_IsRejected()
    {
        var encrypted = Encrypt(Password, Document);
        Encryptor.SetKey(Password + "x");
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(encrypted));
    }

    [Fact]
    public void NoPassword_NothingIsEncryptedOrDecrypted()
    {
        var encrypted = Encrypt(Password, Document);
        Encryptor.RemoveKey();
        Assert.Null(Encryptor.EncryptData(Document));
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(encrypted));
    }

    [Theory]
    [InlineData(4)] // Format version
    [InlineData(9)] // Iterations
    [InlineData(10)] // Salt
    [InlineData(26)] // Nonce
    [InlineData(38)] // Tag
    [InlineData(54)] // First byte of content
    [InlineData(-1)] // Last byte of content
    public void AlteredFile_IsRejected(int position)
    {
        var encrypted = Encrypt(Password, Document);
        encrypted[position < 0 ? encrypted.Length + position : position] ^= 0x01;
        Encryptor.SetKey(Password);
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(encrypted));
    }

    [Fact]
    public void TruncatedFile_IsRejected()
    {
        var encrypted = Encrypt(Password, Document);
        Encryptor.SetKey(Password);
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(encrypted[..^1]));
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(encrypted[..40]));
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(Array.Empty<byte>()));
    }

    [Fact]
    public void EverySave_UsesNewNonce_AndEveryPasswordChange_NewSalt()
    {
        var first = Encrypt(Password, Document);
        var second = Encryptor.EncryptData(Document)!; // Same session
        Assert.Equal(first[10..26], second[10..26]); // Salt is kept within the session
        Assert.NotEqual(first[26..38], second[26..38]); // Nonce is not
        Assert.NotEqual(first[54..], second[54..]);

        var third = Encrypt(Password, Document); // Password set again
        Assert.NotEqual(first[10..26], third[10..26]);
    }

    [Fact]
    public void SaveAfterLogin_CanBeReadWithSamePassword()
    {
        var encrypted = Encrypt(Password, Document);
        Encryptor.SetKey(Password);
        Assert.Equal(Document, Encryptor.DecryptData(encrypted));
        var savedAgain = Encryptor.EncryptData(Document + "more")!;
        Encryptor.SetKey(Password);
        Assert.Equal(Document + "more", Encryptor.DecryptData(savedAgain));
    }

    [Fact]
    public void NonAsciiPasswords_AreDistinguished()
    {
        // The legacy format reduced all non-ASCII characters to "?"
        var encrypted = Encrypt("pässwörd-æøå", Document);
        Encryptor.SetKey("p?ssw?rd-???");
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(encrypted));
        Encryptor.SetKey("pässwörd-æøå");
        Assert.Equal(Document, Encryptor.DecryptData(encrypted));
    }

    [Fact]
    public void IsCurrentKey_OnlyMatchesTheCurrentPassword()
    {
        Encryptor.SetKey(Password);
        Assert.True(Encryptor.IsCurrentKey(Password));
        Assert.False(Encryptor.IsCurrentKey(Password + "x"));
        Encryptor.RemoveKey();
        Assert.False(Encryptor.IsCurrentKey(string.Empty));
    }
}

/// <summary>
/// Creates data files exactly like Marbles v1.0.1.3 and earlier did (copied from the original Encryptor)
/// </summary>
internal static class LegacyFileWriter
{
    internal static byte[] Create(string document, string password)
    {
        var key = password;
        while (key.Length < 32)
        {
            key += key;
        }

        key = key[..32];
        var textbytes = Encoding.UTF8.GetBytes(document);
        var endec = Aes.Create();
        endec.BlockSize = 128;
        endec.KeySize = 256;
        endec.IV = Encoding.ASCII.GetBytes("0pN-391xnqr2a5dv");
        endec.Key = Encoding.ASCII.GetBytes(key);
        endec.Padding = PaddingMode.PKCS7;
        endec.Mode = CipherMode.CBC;
        var icrypt = endec.CreateEncryptor(endec.Key, endec.IV);
        var enc = icrypt.TransformFinalBlock(textbytes, 0, textbytes.Length);
        // File.WriteAllText(path, base64) wrote UTF-8 without BOM
        return Encoding.UTF8.GetBytes(Convert.ToBase64String(enc));
    }
}

[Collection("Static state")]
public class LegacyEncryptorTests
{
    private const string Password = "oldpassword";
    private const string Document = FileIo.XmlDoctype + "<entries><entry><id>1</id></entry></entries>";

    [Fact]
    public void LegacyFile_IsDecryptedAndFlagged()
    {
        Encryptor.SetKey(Password);
        Assert.Equal(Document, Encryptor.DecryptData(LegacyFileWriter.Create(Document, Password)));
        Assert.True(Encryptor.LegacyFormatDetected);
    }

    [Fact]
    public void EmptyLegacyFile_IsDecrypted()
    {
        Encryptor.SetKey(Password);
        Assert.Equal(DialogText.NoData, Encryptor.DecryptData(LegacyFileWriter.Create(DialogText.NoData, Password)));
        Assert.True(Encryptor.LegacyFormatDetected);
    }

    [Fact]
    public void LegacyFile_WithWrongPassword_IsRejected()
    {
        Encryptor.SetKey("wrongpassword");
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(LegacyFileWriter.Create(Document, Password)));
        Assert.False(Encryptor.LegacyFormatDetected);
    }

    [Fact]
    public void RandomContent_IsRejected()
    {
        Encryptor.SetKey(Password);
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(Encoding.UTF8.GetBytes("not a data file")));
        Assert.Equal(DialogText.InvalidData, Encryptor.DecryptData(RandomNumberGenerator.GetBytes(256)));
    }
}

[Collection("Static state")]
public class FileIoTests : IDisposable
{
    private const string Password = "oldpassword";
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    private readonly string _workDirectory =
        Path.Combine(Path.GetTempPath(), "marbles-tests-" + Guid.NewGuid().ToString("N"));

    private string DataFile => Path.Combine(_workDirectory, "data", "data.mbls");

    public FileIoTests()
    {
        // The data directory is always relative to the current directory
        Directory.CreateDirectory(Path.Combine(_workDirectory, "data"));
        Directory.SetCurrentDirectory(_workDirectory);
        FileIo.ResetSubFolder();
        FileIo.SetDefaultFileName();
        FileIo.SetBackup(false, string.Empty, false);
        Entries.ClearAllEntries(false);
    }

    public void Dispose()
    {
        Encryptor.RemoveKey();
        Entries.ClearAllEntries(false);
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_workDirectory, true);
    }

    private static void Login(string password)
    {
        Entries.ClearAllEntries(false);
        Encryptor.SetKey(password);
    }

    private static List<(string, string, string, string, string, int, string)> Snapshot()
    {
        return Entries.GetEntries()
            .Select(e => (e.Id, e.DateCreated, e.DateChanged, e.Title, e.Text, e.CursorIndex, e.Options)).ToList();
    }

    [Fact]
    public void SaveAndLoad_RoundTripsEntries()
    {
        Login(Password);
        Entries.NewEntry("Title æøå", "Line 1\nLine 2 ]]> <tag> & 日本語");
        Entries.NewEntry("Second", string.Empty);
        Assert.True(FileIo.SaveEntries().Saved);
        Assert.False(File.Exists(DataFile + ".tmp"));

        Login(Password);
        var load = FileIo.LoadEntries();
        Assert.True(load.Success);
        Assert.False(load.IsLegacyFormat);
        Assert.Equal(2, Entries.GetEntriesCount());
        Assert.Equal("Title æøå", Entries.GetEntries()[0].Title);
        Assert.Equal("Line 1\nLine 2 ]]> <tag> & 日本語", Entries.GetEntries()[0].Text);
    }

    [Fact]
    public void Load_WithWrongPassword_Fails()
    {
        Login(Password);
        Entries.NewEntry("Title", "Text");
        FileIo.SaveEntries();

        Login("wrongpassword");
        var load = FileIo.LoadEntries();
        Assert.False(load.Success);
        Assert.Contains(DialogText.InvalidPassword, load.StatusMessage);
        Assert.Equal(0, Entries.GetEntriesCount());
    }

    [Fact]
    public void Load_MissingFile_ReportsFileNotFound()
    {
        Login(Password);
        var load = FileIo.LoadEntries();
        Assert.False(load.Success);
        Assert.Equal(DialogText.FileNotFound, load.StatusMessage);
    }

    [Fact]
    public void Save_WithoutPassword_LeavesExistingFileUntouched()
    {
        Login(Password);
        Entries.NewEntry("Title", "Text");
        FileIo.SaveEntries();
        var before = File.ReadAllBytes(DataFile);

        Encryptor.RemoveKey();
        Assert.Equal(DialogText.StatusUnableToSave, FileIo.SaveEntries().Error);
        Assert.Equal(before, File.ReadAllBytes(DataFile));
    }

    [Fact]
    public void LegacyFile_IsConvertedAndOriginalIsKept()
    {
        // Create a legacy file from a document written by the current XML writer
        Login(Password);
        Entries.NewEntry("Old title", "Old text æøå\nline 2");
        Entries.NewEntry("Old title 2", "More");
        Entries.GetEntries()[1].Options = "fp1";
        Entries.GetEntries()[1].CursorIndex = 3;
        var expected = Snapshot();
        FileIo.SaveEntries();
        var document = Encryptor.DecryptData(File.ReadAllBytes(DataFile));
        var legacyFile = LegacyFileWriter.Create(document, Password);
        File.WriteAllBytes(DataFile, legacyFile);

        // Log in to the legacy file
        Login(Password);
        var load = FileIo.LoadEntries();
        Assert.True(load.Success);
        Assert.True(load.IsLegacyFormat);
        Assert.True(FileIo.ConvertLegacyFile().Saved);

        // The original is kept as a copy and the data file is now in the current format
        Assert.Equal("data.mbls.legacy", FileIo.LegacyCopyFileName);
        Assert.Equal(legacyFile, File.ReadAllBytes(DataFile + ".legacy"));
        Assert.Equal("MBLS", Encoding.ASCII.GetString(File.ReadAllBytes(DataFile), 0, 4));

        // Log in again
        Login(Password);
        load = FileIo.LoadEntries();
        Assert.True(load.Success);
        Assert.False(load.IsLegacyFormat);
        Assert.Equal(expected, Snapshot());
    }

    [Theory]
    [InlineData("17-11-2021 12:21:46", "2021-11-17 12:21:46")]
    [InlineData("1-2-2019 7:05:09", "2019-02-01 07:05:09")]
    [InlineData("03.04.2020 18:30", "2020-04-03 18:30:00")]
    [InlineData("29/02/2020", "2020-02-29 00:00:00")]
    // Already as it should be, or not a date with the day first: left alone
    [InlineData("2021-11-17 12:21:46", null)]
    [InlineData("11-17-2021 12:21:46", null)] // Not a month
    [InlineData("31-02-2021 12:21:46", null)] // Not a day of the month
    [InlineData("17-11-2021 25:00:00", null)]
    [InlineData("17-11-21 12:21:46", null)]
    [InlineData("[Cleared]", null)]
    [InlineData("", null)]
    public void ATimestampWithTheDayFirst_IsTurnedAround(string timestamp, string? expected)
    {
        Assert.Equal(expected ?? timestamp, IsoDateTime.CorrectDayFirst(timestamp, out var corrected));
        Assert.Equal(expected != null, corrected);
    }

    [Fact]
    public void LegacyConversion_TurnsTimestampsWithTheDayFirstAround()
    {
        Login(Password);
        Entries.NewEntry("Old", "Text");
        Entries.NewEntry("Newer", "Text");
        Entries.GetEntries()[0].DateCreated = "17-11-2021 12:21:46";
        Entries.GetEntries()[0].DateChanged = "05-01-2022 08:00:01";
        var kept = Entries.GetEntries()[1].DateCreated;
        FileIo.SaveEntries();
        var document = Encryptor.DecryptData(File.ReadAllBytes(DataFile));
        File.WriteAllBytes(DataFile, LegacyFileWriter.Create(document, Password));

        Login(Password);
        Assert.True(FileIo.LoadEntries().IsLegacyFormat);
        Assert.Equal("17-11-2021 12:21:46", Entries.GetEntries()[0].DateCreated);
        Assert.True(FileIo.ConvertLegacyFile().Saved);

        // The converted file has the dates turned around, and the others as they were
        Login(Password);
        Assert.True(FileIo.LoadEntries().Success);
        Assert.Equal("2021-11-17 12:21:46", Entries.GetEntries()[0].DateCreated);
        Assert.Equal("2022-01-05 08:00:01", Entries.GetEntries()[0].DateChanged);
        Assert.Equal(kept, Entries.GetEntries()[1].DateCreated);
        // Nothing left to correct
        Assert.Equal(0, FileIo.CorrectTimestamps(Entries.GetEntries()));
    }

    [Fact]
    public void LegacyConversion_NeverOverwritesAnEarlierCopy()
    {
        File.WriteAllBytes(DataFile, LegacyFileWriter.Create(DialogText.NoData, Password));
        File.WriteAllText(DataFile + ".legacy", "earlier copy");

        Login(Password);
        var load = FileIo.LoadEntries();
        Assert.True(load.Success);
        Assert.True(load.IsLegacyFormat);
        Assert.True(FileIo.ConvertLegacyFile().Saved);
        Assert.Equal("earlier copy", File.ReadAllText(DataFile + ".legacy"));
        Assert.NotEqual("data.mbls.legacy", FileIo.LegacyCopyFileName);
        Assert.True(File.Exists(Path.Combine(_workDirectory, "data", FileIo.LegacyCopyFileName)));
    }

    [Fact]
    public void File_WithUnreadableDocument_IsNotLoadedAsEmpty()
    {
        // Decrypts fine but the XML is damaged: continuing would save an empty file over it
        File.WriteAllBytes(DataFile, LegacyFileWriter.Create(FileIo.XmlDoctype + "<entries><entry>", Password));
        Login(Password);
        var load = FileIo.LoadEntries();
        Assert.False(load.Success);
        Assert.Equal(DialogText.ErrorDataDamaged, load.StatusMessage);
    }

    [Fact]
    public void Entries_WithCharactersNotAllowedInXml_AreSavedWithoutThem()
    {
        Login(Password);
        Entries.NewEntry("Title\u0001\u000B", "Form\ffeed, escape\u001b, tab\t, emoji \U0001F600, lone \uD83D surrogate");
        Assert.True(FileIo.SaveEntries().Saved);

        Login(Password);
        Assert.True(FileIo.LoadEntries().Success);
        Assert.Equal("Title", Entries.GetEntries()[0].Title);
        Assert.Equal("Formfeed, escape, tab\t, emoji \U0001F600, lone  surrogate", Entries.GetEntries()[0].Text);
    }

    [Fact]
    public void LegacyFile_WithCharactersNotAllowedInXml_IsRecovered()
    {
        // Earlier versions saved such characters, and then loaded the file as empty
        var document = FileIo.XmlDoctype + "<entries><entry><id>1</id><created>2022-01-01 10:00:00</created>" +
                       "<changed>2022-01-02 10:00:00</changed><title><![CDATA[Title]]></title>" +
                       "<text><![CDATA[page 1\fpage 2]]></text><cindex>3</cindex><options>f</options></entry></entries>";
        File.WriteAllBytes(DataFile, LegacyFileWriter.Create(document, Password));
        Login(Password);
        var load = FileIo.LoadEntries();
        Assert.True(load.Success);
        Assert.True(load.IsLegacyFormat);
        Assert.Equal("page 1page 2", Entries.GetEntries()[0].Text);
        // The reason: the XML reader refuses the document as it is
        Assert.Throws<System.Xml.XmlException>(() => new System.Xml.XmlDocument().LoadXml(document));
        Assert.Equal("f", Entries.GetEntries()[0].Options);
    }

    [Fact]
    public void File_WithMissingElements_IsLoadedWithDefaults()
    {
        // The oldest files have no cursor index
        var document = FileIo.XmlDoctype + "<entries><entry><id>42</id><created>2022-01-01 10:00:00</created>" +
                       "<changed>2022-01-02 10:00:00</changed><title><![CDATA[Title]]></title>" +
                       "<text><![CDATA[Text]]></text></entry><entry><title><![CDATA[Only a title]]></title></entry></entries>";
        File.WriteAllBytes(DataFile, LegacyFileWriter.Create(document, Password));
        Login(Password);
        Assert.True(FileIo.LoadEntries().Success);
        Assert.Equal(2, Entries.GetEntriesCount());
        Assert.Equal("42", Entries.GetEntries()[0].Id);
        Assert.Equal(0, Entries.GetEntries()[0].CursorIndex);
        Assert.Equal("Only a title", Entries.GetEntries()[1].Title);
        Assert.NotEmpty(Entries.GetEntries()[1].Id);
    }

    [Fact]
    public void EmptyFile_LoadsWithoutEntries_AlsoAfterAnotherFileWasLoaded()
    {
        Login(Password);
        Entries.NewEntry("Title", "Text");
        File.WriteAllBytes(DataFile, LegacyFileWriter.Create(DialogText.NoData, Password));
        Encryptor.SetKey(Password); // Without clearing the entries first
        Assert.True(FileIo.LoadEntries().Success);
        Assert.Equal(0, Entries.GetEntriesCount());
    }

    [Fact]
    public void LongPasswords_AreAccepted()
    {
        var passphrase = string.Join(" ", Enumerable.Repeat("correct horse battery staple", 4));
        Assert.True(Encryptor.IsValidPasswordLength(passphrase));
        Assert.False(Encryptor.IsValidPasswordLength(new string('x', 129)));
        Login(passphrase);
        Entries.NewEntry("Title", "Text");
        Assert.True(FileIo.SaveEntries().Saved);
        Login(passphrase[..^1]);
        Assert.False(FileIo.LoadEntries().Success);
        Login(passphrase);
        Assert.True(FileIo.LoadEntries().Success);
    }

    [Fact]
    public void Backup_OfADataFileInASubfolder_IsInTheSameSubfolder()
    {
        Directory.CreateDirectory(Path.Combine(_workDirectory, "data", "sub"));
        var backup = Path.Combine(_workDirectory, "backup");
        Directory.CreateDirectory(backup);
        FileIo.SetSubFolder("sub");
        FileIo.SetBackup(true, backup, false);
        Login(Password);
        Entries.NewEntry("Title", "Text");

        var result = FileIo.SaveEntries();
        Assert.True(result.Saved);
        Assert.Equal(string.Empty, result.BackupError);
        // A data file with the same name in another folder keeps a backup of its own
        Assert.True(File.Exists(Path.Combine(backup, "sub", "data.mbls")));
        Assert.False(File.Exists(Path.Combine(backup, "data.mbls")));
    }

    [Fact]
    public void Backup_ThatCannotBeWritten_IsToldAbout_AndStaysOn()
    {
        FileIo.SetBackup(true, Path.Combine(_workDirectory, "not-there"), false);
        Login(Password);
        Entries.NewEntry("Title", "Text");

        var result = FileIo.SaveEntries();
        Assert.True(result.Saved);
        Assert.Equal(DialogText.StatusUnableToSaveBackup, result.BackupError);
        Assert.True(FileIo.BackupEnabled());
        // Told once: not at every save
        Assert.Equal(string.Empty, FileIo.SaveEntries().BackupError);
        // The next save tries again, and writes it when the directory is there
        Directory.CreateDirectory(Path.Combine(_workDirectory, "not-there"));
        Assert.Equal(string.Empty, FileIo.SaveEntries().BackupError);
        Assert.True(File.Exists(Path.Combine(_workDirectory, "not-there", "data.mbls")));
        // After a backup that worked, a failure is told again
        Directory.Delete(Path.Combine(_workDirectory, "not-there"), true);
        Assert.Equal(DialogText.StatusUnableToSaveBackup, FileIo.SaveEntries().BackupError);
    }

    [Fact]
    public void Backup_InASubfolderOfTheDataDirectory_IsNotWritten()
    {
        // data/A/data.mbls is another data file: a backup of data/data.mbls in data/A would replace it
        var folder = Path.Combine(_workDirectory, "data", "A");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "data.mbls"), "another data file");
        FileIo.SetBackup(true, folder, false);
        Login(Password);
        Entries.NewEntry("Title", "Text");

        Assert.False(FileIo.CheckBackupDirectory(folder));
        Assert.Equal(DialogText.StatusUnableToSaveBackup, FileIo.SaveEntries().BackupError);
        Assert.Equal("another data file", File.ReadAllText(Path.Combine(folder, "data.mbls")));
    }

    [Fact]
    public void Backup_InTheDataDirectory_NeverReplacesADataFile()
    {
        // data/data.mbls is another data file; the one in use is data/sub/data.mbls, backed up to "data"
        var data = Path.Combine(_workDirectory, "data");
        File.WriteAllText(DataFile, "another data file");
        Directory.CreateDirectory(Path.Combine(data, "sub"));
        FileIo.SetSubFolder("sub");
        FileIo.SetBackup(true, data, false);
        Login(Password);
        Entries.NewEntry("Title", "Text");

        // The backup would be the data file itself: it is not written
        Assert.False(FileIo.CheckBackupDirectory(data));
        var result = FileIo.SaveEntries();
        Assert.True(result.Saved);
        Assert.Equal(DialogText.StatusUnableToSaveBackup, result.BackupError);
        Assert.Equal("another data file", File.ReadAllText(DataFile));
        Login(Password);
        Assert.True(FileIo.LoadEntries().Success);
        Assert.Equal(1, Entries.GetEntriesCount());
    }

    [Fact]
    public void ADataFileSavedInAnotherWindow_IsFound()
    {
        // A new data file has nothing to replace
        Login(Password);
        Entries.NewEntry("Title", "Text");
        Assert.False(FileIo.ChangedElsewhere());
        Assert.True(FileIo.SaveEntries().Saved);
        Assert.False(FileIo.ChangedElsewhere());

        // Loaded here, then saved in another window (with another entry)
        Login(Password);
        Assert.True(FileIo.LoadEntries().Success);
        Assert.False(FileIo.ChangedElsewhere());
        var here = File.ReadAllBytes(DataFile);
        Entries.NewEntry("Other window", "Text");
        Assert.True(FileIo.SaveEntries().Saved);
        File.WriteAllBytes(DataFile, here); // As loaded here...
        File.WriteAllBytes(DataFile, File.ReadAllBytes(DataFile).Concat(new byte[] { 0 }).ToArray()); // ...changed
        Assert.True(FileIo.ChangedElsewhere());

        // Saving here (when asked to) makes this the version known
        Assert.True(FileIo.SaveEntries().Saved);
        Assert.False(FileIo.ChangedElsewhere());
        // Logged out: nothing is known
        File.SetLastWriteTimeUtc(DataFile, DateTime.UtcNow.AddMinutes(1));
        FileIo.ForgetKnownVersion();
        Assert.False(FileIo.ChangedElsewhere());
    }

    [Fact]
    public void AnEntryAddedWhereItsIdIsTaken_GetsAnIdOfItsOwn()
    {
        // As a transfer completed into the data file it came from: the copy is an entry of its own
        Entries.NewEntry("Title", "Text");
        var original = Entries.GetEntries()[0];
        Entries.AddEntryToEntries(original, true);
        Assert.Equal(2, Entries.GetEntriesCount());
        Assert.NotEqual(Entries.GetEntries()[0].Id, Entries.GetEntries()[1].Id);
        Assert.Equal("Title", Entries.GetEntries()[1].Title);
        // Where the ID is free, it is kept
        Entries.ClearAllEntries(false);
        Entries.AddEntryToEntries(original, true);
        Assert.Equal(original.Id, Entries.GetEntries()[0].Id);
    }

    [Fact]
    public void Save_ThatFails_IsNotSaved_AndWritesNoBackup()
    {
        var backup = Path.Combine(_workDirectory, "backup");
        Directory.CreateDirectory(backup);
        FileIo.SetBackup(true, backup, false);
        Encryptor.RemoveKey();
        var result = FileIo.SaveEntries();
        Assert.False(result.Saved);
        Assert.Equal(DialogText.StatusUnableToSave, result.Error);
        Assert.False(File.Exists(Path.Combine(backup, "data.mbls")));
    }
}

public class PasswordGeneratorTests
{
    [Fact]
    public void GeneratedPasswords_HaveExpectedShapeAndDiffer()
    {
        var generator = new PasswordGenerator();
        var passwords = Enumerable.Range(0, 50).Select(_ => generator.GeneratePassword()).ToList();
        Assert.All(passwords, p =>
        {
            Assert.Equal(14, p.Length);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, c => ".-!+".Contains(c));
        });
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }
}
