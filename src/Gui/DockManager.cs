
using System.Numerics;
using ImGuiNET;

namespace VrmImpl.Gui;

public class DockManager
{
    private readonly List<Dock<uint>> _docks = [];
    private bool _opt_fullscreen = true;
    private bool _opt_padding = false;
    private ImGuiDockNodeFlags _dockspace_flags = ImGuiDockNodeFlags.PassthruCentralNode;

    public DockManager() { }

    public void AddDock(Dock<uint> dock)
    {
        _docks.Add(dock);
    }

    public void AddDock(string name, string shortCut, DrawDelegate<uint> draw)
    {
        AddDock(new(name, shortCut, draw));
    }

    public void Draw(uint imageIndex)
    {
        BeginDockSpace("DOCK_SPACE");

        if (ImGui.BeginMenuBar())
        {
            // if (ImGui.BeginMenu("File"))
            // {
            //     // static auto filters = ".*,.vrm,.glb,.gltf,.fbx,.bvh,.vrma,.hdr";
            //     // if (ImGui.MenuItem("Open", "")) {
            //     //   ImGuiFileDialog::Instance()->OpenDialog(
            //     //     OPEN_FILE_DIALOG,
            //     //     "Open",
            //     //     filters,
            //     //     m_fileDialogCurrent.string().c_str());
            //     // }
            //
            //     // if (ImGui.MenuItem("Save", "")) {
            //     //   ImGuiFileDialog::Instance()->OpenDialog(
            //     //     SAVE_FILE_DIALOG,
            //     //     "Save",
            //     //     filters,
            //     //     m_fileDialogCurrent.string().c_str(),
            //     //     "out",
            //     //     1,
            //     //     nullptr,
            //     //     ImGuiFileDialogFlags_ConfirmOverwrite);
            //     // }
            //     ImGui.EndMenu();
            // }

            if (ImGui.BeginMenu("Docks"))
            {
                foreach (var dock in _docks)
                {
                    ImGui.MenuItem(dock.MenuLabel, dock.MenuShortCut, ref dock.IsOpen);
                }
                ImGui.EndMenu();
            }

            // if (ImGui.BeginMenu("Help"))
            // {
            //     // ImGui.MenuItem("Version", PACKAGE_VERSION);
            //     ImGui.EndMenu();
            // }

            ImGui.EndMenuBar();
        }

        foreach (var dock in _docks)
        {
            dock.Draw(imageIndex);
        }

        EndDockSpace();
    }

    private static void EndDockSpace()
    {
        ImGui.End();
    }

    void BeginDockSpace(string dock_space)
    {
        // If you strip some features of, this demo is pretty much equivalent to
        // calling DockSpaceOverViewport()! In most cases you should be able to just
        // call DockSpaceOverViewport() and ignore all the code below! In this
        // specific demo, we are not using DockSpaceOverViewport() because:
        // - we allow the host window to be floating/moveable instead of filling the
        // viewport (when opt_fullscreen == false)
        // - we allow the host window to have padding (when opt_padding == true)
        // - we have a local  bar in the host window (vs. you could use
        // BeginMainMenuBar() + DockSpaceOverViewport() in your code!) TL;DR; this
        // demo is more complicated than what you would normally use. If we removed
        // all the options we are showcasing, this demo would become:
        //     void ShowExampleAppDockSpace()
        //     {
        //         ImGui::DockSpaceOverViewport(ImGui::GetMainViewport());
        //     }

        // We are using the ImGuiWindowFlags_NoDocking flag to make the parent window
        // not dockable into, because it would be confusing to have two docking
        // targets within each others.
        ImGuiWindowFlags window_flags = ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoDocking;
        if (_opt_fullscreen)
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            window_flags |=
                ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoCollapse
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove;
            window_flags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus;
        }
        else
        {
            _dockspace_flags &= ~ImGuiDockNodeFlags.PassthruCentralNode;
        }

        // When using ImGuiDockNodeFlags_PassthruCentralNode, DockSpace() will render
        // our background and handle the pass-thru hole, so we ask Begin() to not
        // render a background.
        if (_dockspace_flags.HasFlag(ImGuiDockNodeFlags.PassthruCentralNode))
            window_flags |= ImGuiWindowFlags.NoBackground;

        // Important: note that we proceed even if Begin() returns false (aka window
        // is collapsed). This is because we want to keep our DockSpace() active. If a
        // DockSpace() is inactive, all active windows docked into it will lose their
        // parent and become undocked. We cannot preserve the docking relationship
        // between an active window and an inactive docking, otherwise any change of
        // dockspace/settings would lead to windows being stuck in limbo and never
        // being visible.
        if (!_opt_padding)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0.0f, 0.0f));
        ImGui.Begin(dock_space, window_flags);
        if (!_opt_padding)
            ImGui.PopStyleVar();

        if (_opt_fullscreen)
            ImGui.PopStyleVar(2);

        // Submit the DockSpace
        var io = ImGui.GetIO();
        if (!io.ConfigFlags.HasFlag(ImGuiConfigFlags.DockingEnable))
        {
            throw new Exception();
        }
        var dockspace_id = ImGui.GetID(dock_space);

        ImGui.DockSpace(dockspace_id, new Vector2(0.0f, 0.0f), _dockspace_flags);
    }
}
