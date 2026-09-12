using ImGuiNET;

namespace VrmImpl.Gui;

public class ImGuiContext : IDisposable
{
    public ImGuiContext()
    {
        // IMGUI_CHECKVERSION();
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard; // Enable Keyboard Controls
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad; // Enable Gamepad Controls

        // Setup Dear ImGui style
        ImGui.StyleColorsDark();
        //ImGui.StyleColorsLight();
    }

    public void Dispose()
    {
        ImGui.DestroyContext();
    }
}
