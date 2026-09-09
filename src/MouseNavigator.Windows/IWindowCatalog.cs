using MouseNavigator.Contracts;
using MouseNavigator.Core;
namespace MouseNavigator.Windows;
public interface IWindowCatalog
{
    ApplicationContext Capture(nint hwnd);
    IReadOnlyList<WindowCandidate> Enumerate();
}
