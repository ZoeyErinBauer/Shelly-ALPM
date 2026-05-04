using System.Threading.Tasks;
using Gtk; // Certifique-se de ser Gtk puro, não Gtk.Internal

namespace Shelly.Gtk.Services;

public interface IPkgBuildService
{
    // Agora a assinatura está completa e reconhecível pelo compilador
    Task ShowPreviewAsync(Overlay overlay, string packageName);
}