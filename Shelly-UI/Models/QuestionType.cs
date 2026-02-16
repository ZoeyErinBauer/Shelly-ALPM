namespace Shelly_UI.Models;

public enum QuestionType
{
    InstallIgnorePkg = 1,
    ReplacePkg = 2,
    ConflictPkg = 4,
    CorruptedPkg = 8,
    ImportKey = 16,
    SelectProvider = 32
}
