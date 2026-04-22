using System.Collections.Generic;
using System.Threading.Tasks;

namespace PackageManager.Alpm.Pacfile;

public interface IPacfileManager
{
    Task SavePacfile(PacfileRecord pacfile);
    Task<PacfileRecord?> GetPacfile(string name);
    Task<List<PacfileRecord>> GetPacfiles();
}