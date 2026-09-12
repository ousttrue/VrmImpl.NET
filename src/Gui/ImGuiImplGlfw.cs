// https://github.com/ocornut/imgui/blob/v1.91.6-docking/backends/imgui_impl_glfw.cpp

using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.GLFW;

namespace VrmImpl.Gui;

public unsafe class ImGuiImplGlfw : IDisposable
{
    static readonly Glfw glfw;

    static ImGuiImplGlfw()
    {
        glfw = GlfwProvider.GLFW.Value;
    }

    // GLFW data
    enum GlfwClientApi
    {
        Unknown,
        OpenGL,
        Vulkan,
    };

    WindowHandle* Window;
    GlfwClientApi ClientApi;

    double Time;

    WindowHandle* MouseWindow;

    Cursor*[] MouseCursors = new Cursor*[(int)ImGuiMouseCursor.COUNT];

    //     bool                    MouseIgnoreButtonUpWaitForFocusLoss;
    bool MouseIgnoreButtonUp;

    Vector2 LastValidMousePos;

    //     WindowHandle*             KeyOwnerWindows[GLFW_KEY_LAST];
    bool InstalledCallbacks;

    bool CallbacksChainForAllWindows;
    bool WantUpdateMonitors;

    // Chain GLFW callbacks: our callbacks will call the user's previously installed callbacks, if any.
    GlfwCallbacks.WindowFocusCallback? PrevUserCallbackWindowFocus;

    GlfwCallbacks.CursorPosCallback? PrevUserCallbackCursorPos;
    GlfwCallbacks.CursorEnterCallback? PrevUserCallbackCursorEnter;
    GlfwCallbacks.MouseButtonCallback? PrevUserCallbackMousebutton;
    GlfwCallbacks.ScrollCallback? PrevUserCallbackScroll;
    GlfwCallbacks.KeyCallback? PrevUserCallbackKey;
    GlfwCallbacks.CharCallback? PrevUserCallbackChar;
    GlfwCallbacks.MonitorCallback? PrevUserCallbackMonitor;

    // #ifdef _WIN32
    //     WNDPROC                 PrevWndProc;
    // #endif

    // Functions

    // Not static to allow third-party code to use that if they want to (but undocumented)
    // ImGuiKey ImGui_ImplGlfw_KeyToImGuiKey(int keycode, int scancode)
    // {
    //     IM_UNUSED(scancode);
    //     switch (keycode)
    //     {
    //         case GLFW_KEY_TAB: return ImGuiKey_Tab;
    //         case GLFW_KEY_LEFT: return ImGuiKey_LeftArrow;
    //         case GLFW_KEY_RIGHT: return ImGuiKey_RightArrow;
    //         case GLFW_KEY_UP: return ImGuiKey_UpArrow;
    //         case GLFW_KEY_DOWN: return ImGuiKey_DownArrow;
    //         case GLFW_KEY_PAGE_UP: return ImGuiKey_PageUp;
    //         case GLFW_KEY_PAGE_DOWN: return ImGuiKey_PageDown;
    //         case GLFW_KEY_HOME: return ImGuiKey_Home;
    //         case GLFW_KEY_END: return ImGuiKey_End;
    //         case GLFW_KEY_INSERT: return ImGuiKey_Insert;
    //         case GLFW_KEY_DELETE: return ImGuiKey_Delete;
    //         case GLFW_KEY_BACKSPACE: return ImGuiKey_Backspace;
    //         case GLFW_KEY_SPACE: return ImGuiKey_Space;
    //         case GLFW_KEY_ENTER: return ImGuiKey_Enter;
    //         case GLFW_KEY_ESCAPE: return ImGuiKey_Escape;
    //         case GLFW_KEY_APOSTROPHE: return ImGuiKey_Apostrophe;
    //         case GLFW_KEY_COMMA: return ImGuiKey_Comma;
    //         case GLFW_KEY_MINUS: return ImGuiKey_Minus;
    //         case GLFW_KEY_PERIOD: return ImGuiKey_Period;
    //         case GLFW_KEY_SLASH: return ImGuiKey_Slash;
    //         case GLFW_KEY_SEMICOLON: return ImGuiKey_Semicolon;
    //         case GLFW_KEY_EQUAL: return ImGuiKey_Equal;
    //         case GLFW_KEY_LEFT_BRACKET: return ImGuiKey_LeftBracket;
    //         case GLFW_KEY_BACKSLASH: return ImGuiKey_Backslash;
    //         case GLFW_KEY_RIGHT_BRACKET: return ImGuiKey_RightBracket;
    //         case GLFW_KEY_GRAVE_ACCENT: return ImGuiKey_GraveAccent;
    //         case GLFW_KEY_CAPS_LOCK: return ImGuiKey_CapsLock;
    //         case GLFW_KEY_SCROLL_LOCK: return ImGuiKey_ScrollLock;
    //         case GLFW_KEY_NUM_LOCK: return ImGuiKey_NumLock;
    //         case GLFW_KEY_PRINT_SCREEN: return ImGuiKey_PrintScreen;
    //         case GLFW_KEY_PAUSE: return ImGuiKey_Pause;
    //         case GLFW_KEY_KP_0: return ImGuiKey_Keypad0;
    //         case GLFW_KEY_KP_1: return ImGuiKey_Keypad1;
    //         case GLFW_KEY_KP_2: return ImGuiKey_Keypad2;
    //         case GLFW_KEY_KP_3: return ImGuiKey_Keypad3;
    //         case GLFW_KEY_KP_4: return ImGuiKey_Keypad4;
    //         case GLFW_KEY_KP_5: return ImGuiKey_Keypad5;
    //         case GLFW_KEY_KP_6: return ImGuiKey_Keypad6;
    //         case GLFW_KEY_KP_7: return ImGuiKey_Keypad7;
    //         case GLFW_KEY_KP_8: return ImGuiKey_Keypad8;
    //         case GLFW_KEY_KP_9: return ImGuiKey_Keypad9;
    //         case GLFW_KEY_KP_DECIMAL: return ImGuiKey_KeypadDecimal;
    //         case GLFW_KEY_KP_DIVIDE: return ImGuiKey_KeypadDivide;
    //         case GLFW_KEY_KP_MULTIPLY: return ImGuiKey_KeypadMultiply;
    //         case GLFW_KEY_KP_SUBTRACT: return ImGuiKey_KeypadSubtract;
    //         case GLFW_KEY_KP_ADD: return ImGuiKey_KeypadAdd;
    //         case GLFW_KEY_KP_ENTER: return ImGuiKey_KeypadEnter;
    //         case GLFW_KEY_KP_EQUAL: return ImGuiKey_KeypadEqual;
    //         case GLFW_KEY_LEFT_SHIFT: return ImGuiKey_LeftShift;
    //         case GLFW_KEY_LEFT_CONTROL: return ImGuiKey_LeftCtrl;
    //         case GLFW_KEY_LEFT_ALT: return ImGuiKey_LeftAlt;
    //         case GLFW_KEY_LEFT_SUPER: return ImGuiKey_LeftSuper;
    //         case GLFW_KEY_RIGHT_SHIFT: return ImGuiKey_RightShift;
    //         case GLFW_KEY_RIGHT_CONTROL: return ImGuiKey_RightCtrl;
    //         case GLFW_KEY_RIGHT_ALT: return ImGuiKey_RightAlt;
    //         case GLFW_KEY_RIGHT_SUPER: return ImGuiKey_RightSuper;
    //         case GLFW_KEY_MENU: return ImGuiKey_Menu;
    //         case GLFW_KEY_0: return ImGuiKey_0;
    //         case GLFW_KEY_1: return ImGuiKey_1;
    //         case GLFW_KEY_2: return ImGuiKey_2;
    //         case GLFW_KEY_3: return ImGuiKey_3;
    //         case GLFW_KEY_4: return ImGuiKey_4;
    //         case GLFW_KEY_5: return ImGuiKey_5;
    //         case GLFW_KEY_6: return ImGuiKey_6;
    //         case GLFW_KEY_7: return ImGuiKey_7;
    //         case GLFW_KEY_8: return ImGuiKey_8;
    //         case GLFW_KEY_9: return ImGuiKey_9;
    //         case GLFW_KEY_A: return ImGuiKey_A;
    //         case GLFW_KEY_B: return ImGuiKey_B;
    //         case GLFW_KEY_C: return ImGuiKey_C;
    //         case GLFW_KEY_D: return ImGuiKey_D;
    //         case GLFW_KEY_E: return ImGuiKey_E;
    //         case GLFW_KEY_F: return ImGuiKey_F;
    //         case GLFW_KEY_G: return ImGuiKey_G;
    //         case GLFW_KEY_H: return ImGuiKey_H;
    //         case GLFW_KEY_I: return ImGuiKey_I;
    //         case GLFW_KEY_J: return ImGuiKey_J;
    //         case GLFW_KEY_K: return ImGuiKey_K;
    //         case GLFW_KEY_L: return ImGuiKey_L;
    //         case GLFW_KEY_M: return ImGuiKey_M;
    //         case GLFW_KEY_N: return ImGuiKey_N;
    //         case GLFW_KEY_O: return ImGuiKey_O;
    //         case GLFW_KEY_P: return ImGuiKey_P;
    //         case GLFW_KEY_Q: return ImGuiKey_Q;
    //         case GLFW_KEY_R: return ImGuiKey_R;
    //         case GLFW_KEY_S: return ImGuiKey_S;
    //         case GLFW_KEY_T: return ImGuiKey_T;
    //         case GLFW_KEY_U: return ImGuiKey_U;
    //         case GLFW_KEY_V: return ImGuiKey_V;
    //         case GLFW_KEY_W: return ImGuiKey_W;
    //         case GLFW_KEY_X: return ImGuiKey_X;
    //         case GLFW_KEY_Y: return ImGuiKey_Y;
    //         case GLFW_KEY_Z: return ImGuiKey_Z;
    //         case GLFW_KEY_F1: return ImGuiKey_F1;
    //         case GLFW_KEY_F2: return ImGuiKey_F2;
    //         case GLFW_KEY_F3: return ImGuiKey_F3;
    //         case GLFW_KEY_F4: return ImGuiKey_F4;
    //         case GLFW_KEY_F5: return ImGuiKey_F5;
    //         case GLFW_KEY_F6: return ImGuiKey_F6;
    //         case GLFW_KEY_F7: return ImGuiKey_F7;
    //         case GLFW_KEY_F8: return ImGuiKey_F8;
    //         case GLFW_KEY_F9: return ImGuiKey_F9;
    //         case GLFW_KEY_F10: return ImGuiKey_F10;
    //         case GLFW_KEY_F11: return ImGuiKey_F11;
    //         case GLFW_KEY_F12: return ImGuiKey_F12;
    //         case GLFW_KEY_F13: return ImGuiKey_F13;
    //         case GLFW_KEY_F14: return ImGuiKey_F14;
    //         case GLFW_KEY_F15: return ImGuiKey_F15;
    //         case GLFW_KEY_F16: return ImGuiKey_F16;
    //         case GLFW_KEY_F17: return ImGuiKey_F17;
    //         case GLFW_KEY_F18: return ImGuiKey_F18;
    //         case GLFW_KEY_F19: return ImGuiKey_F19;
    //         case GLFW_KEY_F20: return ImGuiKey_F20;
    //         case GLFW_KEY_F21: return ImGuiKey_F21;
    //         case GLFW_KEY_F22: return ImGuiKey_F22;
    //         case GLFW_KEY_F23: return ImGuiKey_F23;
    //         case GLFW_KEY_F24: return ImGuiKey_F24;
    //         default: return ImGuiKey_None;
    //     }
    // }

    // X11 does not include current pressed/released modifier key in 'mods' flags submitted by GLFW
    // See https://github.com/ocornut/imgui/issues/6034 and https://github.com/glfw/glfw/issues/1630
    static unsafe void ImGui_ImplGlfw_UpdateKeyModifiers(WindowHandle* window)
    {
        var io = ImGui.GetIO();
        //     io.AddKeyEvent(ImGuiMod_Ctrl,  (glfwGetKey(window, GLFW_KEY_LEFT_CONTROL) == GLFW_PRESS) || (glfwGetKey(window, GLFW_KEY_RIGHT_CONTROL) == GLFW_PRESS));
        //     io.AddKeyEvent(ImGuiMod_Shift, (glfwGetKey(window, GLFW_KEY_LEFT_SHIFT)   == GLFW_PRESS) || (glfwGetKey(window, GLFW_KEY_RIGHT_SHIFT)   == GLFW_PRESS));
        //     io.AddKeyEvent(ImGuiMod_Alt,   (glfwGetKey(window, GLFW_KEY_LEFT_ALT)     == GLFW_PRESS) || (glfwGetKey(window, GLFW_KEY_RIGHT_ALT)     == GLFW_PRESS));
        //     io.AddKeyEvent(ImGuiMod_Super, (glfwGetKey(window, GLFW_KEY_LEFT_SUPER)   == GLFW_PRESS) || (glfwGetKey(window, GLFW_KEY_RIGHT_SUPER)   == GLFW_PRESS));
    }

    unsafe bool ImGui_ImplGlfw_ShouldChainCallback(WindowHandle* window)
    {
        return CallbacksChainForAllWindows ? true : (window == Window);
    }

    unsafe void ImGui_ImplGlfw_MouseButtonCallback(
        WindowHandle* window,
        MouseButton button,
        InputAction action,
        KeyModifiers mods
    )
    {
        if (PrevUserCallbackMousebutton != null && ImGui_ImplGlfw_ShouldChainCallback(window))
            PrevUserCallbackMousebutton(window, button, action, mods);

        // Workaround for Linux: ignore mouse up events which are following an focus loss following a viewport creation
        if (MouseIgnoreButtonUp && action == InputAction.Release)
            return;

        ImGui_ImplGlfw_UpdateKeyModifiers(window);

        var io = ImGui.GetIO();
        if (button >= 0 && (int)button < (int)ImGuiMouseButton.COUNT)
            io.AddMouseButtonEvent((int)button, action == InputAction.Press);
    }

    unsafe void ImGui_ImplGlfw_ScrollCallback(WindowHandle* window, double xoffset, double yoffset)
    {
        //     ImGui_ImplGlfw_Data* bd = ImGui_ImplGlfw_GetBackendData();
        //     if (PrevUserCallbackScroll != nullptr && ImGui_ImplGlfw_ShouldChainCallback(window))
        //         PrevUserCallbackScroll(window, xoffset, yoffset);

        // #ifdef EMSCRIPTEN_USE_EMBEDDED_GLFW3
        //     // Ignore GLFW events: will be processed in ImGui_ImplEmscripten_WheelCallback().
        //     return;
        // #endif

        //     ImGuiIO& io = ImGui.GetIO();
        //     io.AddMouseWheelEvent((float)xoffset, (float)yoffset);
    }

    // // FIXME: should this be baked into ImGui_ImplGlfw_KeyToImGuiKey()? then what about the values passed to io.SetKeyEventNativeData()?
    // static int ImGui_ImplGlfw_TranslateUntranslatedKey(int key, int scancode)
    // {
    // #if GLFW_HAS_GETKEYNAME && !defined(EMSCRIPTEN_USE_EMBEDDED_GLFW3)
    //     // GLFW 3.1+ attempts to "untranslate" keys, which goes the opposite of what every other framework does, making using lettered shortcuts difficult.
    //     // (It had reasons to do so: namely GLFW is/was more likely to be used for WASD-type game controls rather than lettered shortcuts, but IHMO the 3.1 change could have been done differently)
    //     // See https://github.com/glfw/glfw/issues/1502 for details.
    //     // Adding a workaround to undo this (so our keys are translated.untranslated.translated, likely a lossy process).
    //     // This won't cover edge cases but this is at least going to cover common cases.
    //     if (key >= GLFW_KEY_KP_0 && key <= GLFW_KEY_KP_EQUAL)
    //         return key;
    //     GLFWerrorfun prev_error_callback = glfwSetErrorCallback(nullptr);
    //     const char* key_name = glfwGetKeyName(key, scancode);
    //     glfwSetErrorCallback(prev_error_callback);
    // #if GLFW_HAS_GETERROR && !defined(EMSCRIPTEN_USE_EMBEDDED_GLFW3) // Eat errors (see #5908)
    //     (void)glfwGetError(nullptr);
    // #endif
    //     if (key_name && key_name[0] != 0 && key_name[1] == 0)
    //     {
    //         const char char_names[] = "`-=[]\\,;\'./";
    //         const int char_keys[] = { GLFW_KEY_GRAVE_ACCENT, GLFW_KEY_MINUS, GLFW_KEY_EQUAL, GLFW_KEY_LEFT_BRACKET, GLFW_KEY_RIGHT_BRACKET, GLFW_KEY_BACKSLASH, GLFW_KEY_COMMA, GLFW_KEY_SEMICOLON, GLFW_KEY_APOSTROPHE, GLFW_KEY_PERIOD, GLFW_KEY_SLASH, 0 };
    //         IM_ASSERT(IM_ARRAYSIZE(char_names) == IM_ARRAYSIZE(char_keys));
    //         if (key_name[0] >= '0' && key_name[0] <= '9')               { key = GLFW_KEY_0 + (key_name[0] - '0'); }
    //         else if (key_name[0] >= 'A' && key_name[0] <= 'Z')          { key = GLFW_KEY_A + (key_name[0] - 'A'); }
    //         else if (key_name[0] >= 'a' && key_name[0] <= 'z')          { key = GLFW_KEY_A + (key_name[0] - 'a'); }
    //         else if (const char* p = strchr(char_names, key_name[0]))   { key = char_keys[p - char_names]; }
    //     }
    //     // if (action == GLFW_PRESS) printf("key %d scancode %d name '%s'\n", key, scancode, key_name);
    // #else
    //     IM_UNUSED(scancode);
    // #endif
    //     return key;
    // }

    unsafe void ImGui_ImplGlfw_KeyCallback(
        WindowHandle* window,
        Keys keycode,
        int scancode,
        InputAction action,
        KeyModifiers mods
    )
    {
        //     ImGui_ImplGlfw_Data* bd = ImGui_ImplGlfw_GetBackendData();
        //     if (PrevUserCallbackKey != nullptr && ImGui_ImplGlfw_ShouldChainCallback(window))
        //         PrevUserCallbackKey(window, keycode, scancode, action, mods);

        //     if (action != GLFW_PRESS && action != GLFW_RELEASE)
        //         return;

        //     ImGui_ImplGlfw_UpdateKeyModifiers(window);

        //     if (keycode >= 0 && keycode < IM_ARRAYSIZE(KeyOwnerWindows))
        //         KeyOwnerWindows[keycode] = (action == GLFW_PRESS) ? window : nullptr;

        //     keycode = ImGui_ImplGlfw_TranslateUntranslatedKey(keycode, scancode);

        //     ImGuiIO& io = ImGui.GetIO();
        //     ImGuiKey imgui_key = ImGui_ImplGlfw_KeyToImGuiKey(keycode, scancode);
        //     io.AddKeyEvent(imgui_key, (action == GLFW_PRESS));
        //     io.SetKeyEventNativeData(imgui_key, keycode, scancode); // To support legacy indexing (<1.87 user code)
    }

    unsafe void ImGui_ImplGlfw_WindowFocusCallback(WindowHandle* window, bool focused)
    {
        //     ImGui_ImplGlfw_Data* bd = ImGui_ImplGlfw_GetBackendData();
        //     if (PrevUserCallbackWindowFocus != nullptr && ImGui_ImplGlfw_ShouldChainCallback(window))
        //         PrevUserCallbackWindowFocus(window, focused);

        //     // Workaround for Linux: when losing focus with MouseIgnoreButtonUpWaitForFocusLoss set, we will temporarily ignore subsequent Mouse Up events
        //     MouseIgnoreButtonUp = (MouseIgnoreButtonUpWaitForFocusLoss && focused == 0);
        //     MouseIgnoreButtonUpWaitForFocusLoss = false;

        //     ImGuiIO& io = ImGui.GetIO();
        //     io.AddFocusEvent(focused != 0);
    }

    void ImGui_ImplGlfw_CursorPosCallback(WindowHandle* window, double x, double y)
    {
        if (PrevUserCallbackCursorPos != null && ImGui_ImplGlfw_ShouldChainCallback(window))
            PrevUserCallbackCursorPos(window, x, y);

        var io = ImGui.GetIO();
        if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            glfw.GetWindowPos(window, out var window_x, out var window_y);
            x += window_x;
            y += window_y;
        }
        io.AddMousePosEvent((float)x, (float)y);
        LastValidMousePos = new((float)x, (float)y);
    }

    // Workaround: X11 seems to send spurious Leave/Enter events which would make us lose our position,
    // so we back it up and restore on Leave/Enter (see https://github.com/ocornut/imgui/issues/4984)
    void ImGui_ImplGlfw_CursorEnterCallback(WindowHandle* window, bool entered)
    {
        if (PrevUserCallbackCursorEnter != null && ImGui_ImplGlfw_ShouldChainCallback(window))
            PrevUserCallbackCursorEnter(window, entered);

        var io = ImGui.GetIO();
        if (entered)
        {
            MouseWindow = window;
            io.AddMousePosEvent(LastValidMousePos.X, LastValidMousePos.Y);
        }
        else if (!entered && MouseWindow == window)
        {
            LastValidMousePos = io.MousePos;
            MouseWindow = null;
            io.AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        }
    }

    unsafe void ImGui_ImplGlfw_CharCallback(WindowHandle* window, uint c)
    {
        //     ImGui_ImplGlfw_Data* bd = ImGui_ImplGlfw_GetBackendData();
        //     if (PrevUserCallbackChar != nullptr && ImGui_ImplGlfw_ShouldChainCallback(window))
        //         PrevUserCallbackChar(window, c);

        //     ImGuiIO& io = ImGui.GetIO();
        //     io.AddInputCharacter(c);
    }

    unsafe void ImGui_ImplGlfw_MonitorCallback(Silk.NET.GLFW.Monitor* monitor, ConnectedState state)
    {
        WantUpdateMonitors = true;
    }

    // #ifdef _WIN32
    // static LRESULT CALLBACK ImGui_ImplGlfw_WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);
    // #endif

    void ImGui_ImplGlfw_InstallCallbacks(WindowHandle* window)
    {
        if (InstalledCallbacks)
        {
            throw new Exception("Callbacks already installed!");
        }
        if (Window != window)
        {
            throw new Exception("Different window!");
        }

        PrevUserCallbackWindowFocus = glfw.SetWindowFocusCallback(
            window,
            ImGui_ImplGlfw_WindowFocusCallback
        );
        PrevUserCallbackCursorEnter = glfw.SetCursorEnterCallback(
            window,
            ImGui_ImplGlfw_CursorEnterCallback
        );
        PrevUserCallbackCursorPos = glfw.SetCursorPosCallback(
            window,
            ImGui_ImplGlfw_CursorPosCallback
        );
        PrevUserCallbackMousebutton = glfw.SetMouseButtonCallback(
            window,
            ImGui_ImplGlfw_MouseButtonCallback
        );
        PrevUserCallbackScroll = glfw.SetScrollCallback(window, ImGui_ImplGlfw_ScrollCallback);
        PrevUserCallbackKey = glfw.SetKeyCallback(window, ImGui_ImplGlfw_KeyCallback);
        PrevUserCallbackChar = glfw.SetCharCallback(window, ImGui_ImplGlfw_CharCallback);
        PrevUserCallbackMonitor = glfw.SetMonitorCallback(ImGui_ImplGlfw_MonitorCallback);
        InstalledCallbacks = true;
    }

    void ImGui_ImplGlfw_RestoreCallbacks(WindowHandle* window)
    {
        if (!InstalledCallbacks)
        {
            throw new Exception("Callbacks not installed!");
        }
        if (Window != window)
        {
            throw new Exception("Different window!");
        }

        glfw.SetWindowFocusCallback(window, PrevUserCallbackWindowFocus);
        glfw.SetCursorEnterCallback(window, PrevUserCallbackCursorEnter);
        glfw.SetCursorPosCallback(window, PrevUserCallbackCursorPos);
        glfw.SetMouseButtonCallback(window, PrevUserCallbackMousebutton);
        glfw.SetScrollCallback(window, PrevUserCallbackScroll);
        glfw.SetKeyCallback(window, PrevUserCallbackKey);
        glfw.SetCharCallback(window, PrevUserCallbackChar);
        glfw.SetMonitorCallback(PrevUserCallbackMonitor);
        InstalledCallbacks = false;
        PrevUserCallbackWindowFocus = null;
        PrevUserCallbackCursorEnter = null;
        PrevUserCallbackCursorPos = null;
        PrevUserCallbackMousebutton = null;
        PrevUserCallbackScroll = null;
        PrevUserCallbackKey = null;
        PrevUserCallbackChar = null;
        PrevUserCallbackMonitor = null;
    }

    unsafe ImGuiImplGlfw(WindowHandle* window, bool install_callbacks, GlfwClientApi client_api)
    {
        var io = ImGui.GetIO();
        // IMGUI_CHECKVERSION();
        if (io.BackendPlatformUserData != default)
        {
            throw new Exception("Already initialized a platform backend!");
        }
        //printf("GLFW_VERSION: %d.%d.%d (%d)", GLFW_VERSION_MAJOR, GLFW_VERSION_MINOR, GLFW_VERSION_REVISION, GLFW_VERSION_COMBINED);

        // Setup backend capabilities flags
        // io.BackendPlatformUserData = (void*)bd;
        // io.BackendPlatformName = "imgui_impl_glfw";
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors; // We can honor GetMouseCursor() values (optional)
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos; // We can honor io.WantSetMousePos requests (optional, rarely used)
        io.BackendFlags |= ImGuiBackendFlags.PlatformHasViewports; // We can create multi-viewports on the Platform side (optional)
        // #if GLFW_HAS_MOUSE_PASSTHROUGH || GLFW_HAS_WINDOW_HOVERED
        //     io.BackendFlags |= ImGuiBackendFlags_HasMouseHoveredViewport; // We can call io.AddMouseViewportEvent() with correct data (optional)
        // #endif

        Window = window;
        Time = 0.0;
        WantUpdateMonitors = true;

        var platform_io = ImGui.GetPlatformIO();
        // platform_io.Platform_SetClipboardTextFn = [](ImGuiContext*, const char* text) { glfwSetClipboardString(nullptr, text); };
        // platform_io.Platform_GetClipboardTextFn = [](ImGuiContext*) { return glfwGetClipboardString(nullptr); };

        // Create mouse cursors
        // (By design, on X11 cursors are user configurable and some cursors may be missing. When a cursor doesn't exist,
        // GLFW will emit an error which will often be printed by the app, so we temporarily disable error reporting.
        // Missing cursors will return nullptr and our _UpdateMouseCursor() function will use the Arrow cursor instead.)
        var prev_error_callback = glfw.SetErrorCallback(null);
        //     MouseCursors[ImGuiMouseCursor_Arrow] = glfwCreateStandardCursor(GLFW_ARROW_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_TextInput] = glfwCreateStandardCursor(GLFW_IBEAM_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_ResizeNS] = glfwCreateStandardCursor(GLFW_VRESIZE_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_ResizeEW] = glfwCreateStandardCursor(GLFW_HRESIZE_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_Hand] = glfwCreateStandardCursor(GLFW_HAND_CURSOR);
        // #if GLFW_HAS_NEW_CURSORS
        //     MouseCursors[ImGuiMouseCursor_ResizeAll] = glfwCreateStandardCursor(GLFW_RESIZE_ALL_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_ResizeNESW] = glfwCreateStandardCursor(GLFW_RESIZE_NESW_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_ResizeNWSE] = glfwCreateStandardCursor(GLFW_RESIZE_NWSE_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_NotAllowed] = glfwCreateStandardCursor(GLFW_NOT_ALLOWED_CURSOR);
        // #else
        //     MouseCursors[ImGuiMouseCursor_ResizeAll] = glfwCreateStandardCursor(GLFW_ARROW_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_ResizeNESW] = glfwCreateStandardCursor(GLFW_ARROW_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_ResizeNWSE] = glfwCreateStandardCursor(GLFW_ARROW_CURSOR);
        //     MouseCursors[ImGuiMouseCursor_NotAllowed] = glfwCreateStandardCursor(GLFW_ARROW_CURSOR);
        // #endif
        //     glfwSetErrorCallback(prev_error_callback);
        // #if GLFW_HAS_GETERROR && !defined(__EMSCRIPTEN__) // Eat errors (see #5908)
        //     (void)glfwGetError(nullptr);
        // #endif

        // Chain GLFW callbacks: our callbacks will call the user's previously installed callbacks, if any.
        if (install_callbacks)
            ImGui_ImplGlfw_InstallCallbacks(window);

        // Update monitor a first time during init
        // (note: monitor callback are broken in GLFW 3.2 and earlier, see github.com/glfw/glfw/issues/784)
        ImGui_ImplGlfw_UpdateMonitors();
        glfw.SetMonitorCallback(ImGui_ImplGlfw_MonitorCallback);

        // Set platform dependent data in viewport
        var main_viewport = ImGui.GetMainViewport();
        main_viewport.PlatformHandle = new nint(Window);
        // #ifdef _WIN32
        //     main_viewport.PlatformHandleRaw = glfwGetWin32Window(Window);
        // #elif defined(__APPLE__)
        //     main_viewport.PlatformHandleRaw = (void*)glfwGetCocoaWindow(Window);
        // #else
        //     IM_UNUSED(main_viewport);
        // #endif

        // Windows: register a WndProc hook so we can intercept some messages.
        // #ifdef _WIN32
        //     PrevWndProc = (WNDPROC).GetWindowLongPtrW((HWND)main_viewport.PlatformHandleRaw, GWLP_WNDPROC);
        //     IM_ASSERT(PrevWndProc != nullptr);
        //     .SetWindowLongPtrW((HWND)main_viewport.PlatformHandleRaw, GWLP_WNDPROC, (LONG_PTR)ImGui_ImplGlfw_WndProc);
        // #endif

        ClientApi = client_api;
    }

    public static ImGuiImplGlfw InitForOpenGL(WindowHandle* window, bool install_callbacks)
    {
        return new(window, install_callbacks, GlfwClientApi.OpenGL);
    }

    public static ImGuiImplGlfw InitForVulkan(WindowHandle* window, bool install_callbacks)
    {
        return new(window, install_callbacks, GlfwClientApi.Vulkan);
    }

    // bool ImGui_ImplGlfw_InitForOther(WindowHandle* window, bool install_callbacks)
    // {
    //     return ImGui_ImplGlfw_Init(window, install_callbacks, GlfwClientApi_Unknown);
    // }

    public void Dispose()
    {
        //     ImGui_ImplGlfw_Data* bd = ImGui_ImplGlfw_GetBackendData();
        //     IM_ASSERT(bd != nullptr && "No platform backend to shutdown, or already shutdown?");
        //     ImGuiIO& io = ImGui.GetIO();

        //     if (InstalledCallbacks)
        //         ImGui_ImplGlfw_RestoreCallbacks(Window);
        // #ifdef EMSCRIPTEN_USE_EMBEDDED_GLFW3
        //     if (CanvasSelector)
        //         emscripten_set_wheel_callback(CanvasSelector, nullptr, false, nullptr);
        // #endif

        //     for (ImGuiMouseCursor cursor_n = 0; cursor_n < ImGuiMouseCursor_COUNT; cursor_n++)
        //         glfwDestroyCursor(MouseCursors[cursor_n]);

        //     // Windows: restore our WndProc hook
        // #ifdef _WIN32
        //     ImGuiViewport* main_viewport = ImGui.GetMainViewport();
        //     .SetWindowLongPtrW((HWND)main_viewport.PlatformHandleRaw, GWLP_WNDPROC, (LONG_PTR)PrevWndProc);
        //     PrevWndProc = nullptr;
        // #endif

        //     io.BackendPlatformName = nullptr;
        //     io.BackendPlatformUserData = nullptr;
        //     io.BackendFlags &= ~(ImGuiBackendFlags_HasMouseCursors | ImGuiBackendFlags_HasSetMousePos | ImGuiBackendFlags_HasGamepad | ImGuiBackendFlags_PlatformHasViewports | ImGuiBackendFlags_HasMouseHoveredViewport);
        //     IM_DELETE(bd);
    }

    void ImGui_ImplGlfw_UpdateMouseData()
    {
        var io = ImGui.GetIO();
        var platform_io = ImGui.GetPlatformIO();

        uint mouse_viewport_id = 0;
        var mouse_pos_prev = io.MousePos;
        for (int n = 0; n < platform_io.Viewports.Size; n++)
        {
            var viewport = platform_io.Viewports[n];
            var window = (WindowHandle*)viewport.PlatformHandle;

            var is_window_focused = glfw.GetWindowAttrib(window, WindowAttributeGetter.Focused);
            if (is_window_focused)
            {
                // (Optional) Set OS mouse position from Dear ImGui if requested (rarely used, only when io.ConfigNavMoveSetMousePos is enabled by user)
                // When multi-viewports are enabled, all Dear ImGui positions are same as OS positions.
                if (io.WantSetMousePos)
                    glfw.SetCursorPos(
                        window,
                        (double)(mouse_pos_prev.X - viewport.Pos.X),
                        (double)(mouse_pos_prev.Y - viewport.Pos.Y)
                    );

                // (Optional) Fallback to provide mouse position when focused (ImGui_ImplGlfw_CursorPosCallback already provides this when hovered or captured)
                if (MouseWindow == null)
                {
                    glfw.GetCursorPos(window, out var mouse_x, out var mouse_y);
                    if (io.ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                    {
                        // Single viewport mode: mouse position in client window coordinates (io.MousePos is (0,0) when the mouse is on the upper-left corner of the app window)
                        // Multi-viewport mode: mouse position in OS absolute coordinates (io.MousePos is (0,0) when the mouse is on the upper-left of the primary monitor)
                        glfw.GetWindowPos(window, out var window_x, out var window_y);
                        mouse_x += window_x;
                        mouse_y += window_y;
                    }
                    LastValidMousePos = new((float)mouse_x, (float)mouse_y);
                    io.AddMousePosEvent((float)mouse_x, (float)mouse_y);
                }
            }

            //         // (Optional) When using multiple viewports: call io.AddMouseViewportEvent() with the viewport the OS mouse cursor is hovering.
            //         // If ImGuiBackendFlags_HasMouseHoveredViewport is not set by the backend, Dear imGui will ignore this field and infer the information using its flawed heuristic.
            //         // - [X] GLFW >= 3.3 backend ON WINDOWS ONLY does correctly ignore viewports with the _NoInputs flag (since we implement hit via our WndProc hook)
            //         //       On other platforms we rely on the library fallbacking to its own search when reporting a viewport with _NoInputs flag.
            //         // - [!] GLFW <= 3.2 backend CANNOT correctly ignore viewports with the _NoInputs flag, and CANNOT reported Hovered Viewport because of mouse capture.
            //         //       Some backend are not able to handle that correctly. If a backend report an hovered viewport that has the _NoInputs flag (e.g. when dragging a window
            //         //       for docking, the viewport has the _NoInputs flag in order to allow us to find the viewport under), then Dear ImGui is forced to ignore the value reported
            //         //       by the backend, and use its flawed heuristic to guess the viewport behind.
            //         // - [X] GLFW backend correctly reports this regardless of another viewport behind focused and dragged from (we need this to find a useful drag and drop target).
            //         // FIXME: This is currently only correct on Win32. See what we do below with the WM_NCHITTEST, missing an equivalent for other systems.
            //         // See https://github.com/glfw/glfw/issues/1236 if you want to help in making this a GLFW feature.
            // #if GLFW_HAS_MOUSE_PASSTHROUGH
            //         const bool window_no_input = (viewport.Flags & ImGuiViewportFlags_NoInputs) != 0;
            //         glfwSetWindowAttrib(window, GLFW_MOUSE_PASSTHROUGH, window_no_input);
            // #endif
            // #if GLFW_HAS_MOUSE_PASSTHROUGH || GLFW_HAS_WINDOW_HOVERED
            //         if (glfwGetWindowAttrib(window, GLFW_HOVERED))
            //             mouse_viewport_id = viewport.ID;
            // #else
            //         // We cannot use MouseWindow maintained from CursorEnter/Leave callbacks, because it is locked to the window capturing mouse.
            // #endif
        }

        if (io.BackendFlags.HasFlag(ImGuiBackendFlags.HasMouseHoveredViewport))
            io.AddMouseViewportEvent(mouse_viewport_id);
    }

    void ImGui_ImplGlfw_UpdateMouseCursor()
    {
        var io = ImGui.GetIO();
        if (
            io.ConfigFlags.HasFlag(ImGuiConfigFlags.NoMouseCursorChange)
            || (CursorModeValue)glfw.GetInputMode(Window, CursorStateAttribute.Cursor)
                == CursorModeValue.CursorDisabled
        )
            return;

        var imgui_cursor = ImGui.GetMouseCursor();
        var platform_io = ImGui.GetPlatformIO();
        for (int n = 0; n < platform_io.Viewports.Size; n++)
        {
            var window = (WindowHandle*)platform_io.Viewports[n].PlatformHandle;
            if (imgui_cursor == ImGuiMouseCursor.None || io.MouseDrawCursor)
            {
                // Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
                glfw.SetInputMode(
                    window,
                    CursorStateAttribute.Cursor,
                    CursorModeValue.CursorHidden
                );
            }
            else
            {
                // Show OS mouse cursor
                // FIXME-PLATFORM: Unfocused windows seems to fail changing the mouse cursor with GLFW 3.2, but 3.3 works here.
                glfw.SetCursor(
                    window,
                    MouseCursors[(int)imgui_cursor] != null
                        ? MouseCursors[(int)imgui_cursor]
                        : MouseCursors[(int)ImGuiMouseCursor.Arrow]
                );
                glfw.SetInputMode(
                    window,
                    CursorStateAttribute.Cursor,
                    CursorModeValue.CursorNormal
                );
            }
        }
    }

    // // Update gamepad inputs
    // static inline float Saturate(float v) { return v < 0.0f ? 0.0f : v  > 1.0f ? 1.0f : v; }
    static void ImGui_ImplGlfw_UpdateGamepads()
    {
        //     ImGuiIO& io = ImGui.GetIO();
        //     if ((io.ConfigFlags & ImGuiConfigFlags_NavEnableGamepad) == 0) // FIXME: Technically feeding gamepad shouldn't depend on this now that they are regular inputs.
        //         return;

        //     io.BackendFlags &= ~ImGuiBackendFlags_HasGamepad;
        // #if GLFW_HAS_GAMEPAD_API && !defined(EMSCRIPTEN_USE_EMBEDDED_GLFW3)
        //     GLFWgamepadstate gamepad;
        //     if (!glfwGetGamepadState(GLFW_JOYSTICK_1, &gamepad))
        //         return;
        //     #define MAP_BUTTON(KEY_NO, BUTTON_NO, _UNUSED)          do { io.AddKeyEvent(KEY_NO, gamepad.buttons[BUTTON_NO] != 0); } while (0)
        //     #define MAP_ANALOG(KEY_NO, AXIS_NO, _UNUSED, V0, V1)    do { float v = gamepad.axes[AXIS_NO]; v = (v - V0) / (V1 - V0); io.AddKeyAnalogEvent(KEY_NO, v > 0.10f, Saturate(v)); } while (0)
        // #else
        //     int axes_count = 0, buttons_count = 0;
        //     const float* axes = glfwGetJoystickAxes(GLFW_JOYSTICK_1, &axes_count);
        //     const unsigned char* buttons = glfwGetJoystickButtons(GLFW_JOYSTICK_1, &buttons_count);
        //     if (axes_count == 0 || buttons_count == 0)
        //         return;
        //     #define MAP_BUTTON(KEY_NO, _UNUSED, BUTTON_NO)          do { io.AddKeyEvent(KEY_NO, (buttons_count > BUTTON_NO && buttons[BUTTON_NO] == GLFW_PRESS)); } while (0)
        //     #define MAP_ANALOG(KEY_NO, _UNUSED, AXIS_NO, V0, V1)    do { float v = (axes_count > AXIS_NO) ? axes[AXIS_NO] : V0; v = (v - V0) / (V1 - V0); io.AddKeyAnalogEvent(KEY_NO, v > 0.10f, Saturate(v)); } while (0)
        // #endif
        //     io.BackendFlags |= ImGuiBackendFlags_HasGamepad;
        //     MAP_BUTTON(ImGuiKey_GamepadStart,       GLFW_GAMEPAD_BUTTON_START,          7);
        //     MAP_BUTTON(ImGuiKey_GamepadBack,        GLFW_GAMEPAD_BUTTON_BACK,           6);
        //     MAP_BUTTON(ImGuiKey_GamepadFaceLeft,    GLFW_GAMEPAD_BUTTON_X,              2);     // Xbox X, PS Square
        //     MAP_BUTTON(ImGuiKey_GamepadFaceRight,   GLFW_GAMEPAD_BUTTON_B,              1);     // Xbox B, PS Circle
        //     MAP_BUTTON(ImGuiKey_GamepadFaceUp,      GLFW_GAMEPAD_BUTTON_Y,              3);     // Xbox Y, PS Triangle
        //     MAP_BUTTON(ImGuiKey_GamepadFaceDown,    GLFW_GAMEPAD_BUTTON_A,              0);     // Xbox A, PS Cross
        //     MAP_BUTTON(ImGuiKey_GamepadDpadLeft,    GLFW_GAMEPAD_BUTTON_DPAD_LEFT,      13);
        //     MAP_BUTTON(ImGuiKey_GamepadDpadRight,   GLFW_GAMEPAD_BUTTON_DPAD_RIGHT,     11);
        //     MAP_BUTTON(ImGuiKey_GamepadDpadUp,      GLFW_GAMEPAD_BUTTON_DPAD_UP,        10);
        //     MAP_BUTTON(ImGuiKey_GamepadDpadDown,    GLFW_GAMEPAD_BUTTON_DPAD_DOWN,      12);
        //     MAP_BUTTON(ImGuiKey_GamepadL1,          GLFW_GAMEPAD_BUTTON_LEFT_BUMPER,    4);
        //     MAP_BUTTON(ImGuiKey_GamepadR1,          GLFW_GAMEPAD_BUTTON_RIGHT_BUMPER,   5);
        //     MAP_ANALOG(ImGuiKey_GamepadL2,          GLFW_GAMEPAD_AXIS_LEFT_TRIGGER,     4,      -0.75f,  +1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadR2,          GLFW_GAMEPAD_AXIS_RIGHT_TRIGGER,    5,      -0.75f,  +1.0f);
        //     MAP_BUTTON(ImGuiKey_GamepadL3,          GLFW_GAMEPAD_BUTTON_LEFT_THUMB,     8);
        //     MAP_BUTTON(ImGuiKey_GamepadR3,          GLFW_GAMEPAD_BUTTON_RIGHT_THUMB,    9);
        //     MAP_ANALOG(ImGuiKey_GamepadLStickLeft,  GLFW_GAMEPAD_AXIS_LEFT_X,           0,      -0.25f,  -1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadLStickRight, GLFW_GAMEPAD_AXIS_LEFT_X,           0,      +0.25f,  +1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadLStickUp,    GLFW_GAMEPAD_AXIS_LEFT_Y,           1,      -0.25f,  -1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadLStickDown,  GLFW_GAMEPAD_AXIS_LEFT_Y,           1,      +0.25f,  +1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadRStickLeft,  GLFW_GAMEPAD_AXIS_RIGHT_X,          2,      -0.25f,  -1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadRStickRight, GLFW_GAMEPAD_AXIS_RIGHT_X,          2,      +0.25f,  +1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadRStickUp,    GLFW_GAMEPAD_AXIS_RIGHT_Y,          3,      -0.25f,  -1.0f);
        //     MAP_ANALOG(ImGuiKey_GamepadRStickDown,  GLFW_GAMEPAD_AXIS_RIGHT_Y,          3,      +0.25f,  +1.0f);
        //     #undef MAP_BUTTON
        //     #undef MAP_ANALOG
    }

    unsafe void ImGui_ImplGlfw_UpdateMonitors()
    {
        var platform_io = ImGui.GetPlatformIO();
        WantUpdateMonitors = false;

        var glfw_monitors = glfw.GetMonitors(out var monitors_count);
        if (monitors_count == 0) // Preserve existing monitor list if there are none. Happens on macOS sleeping (#5683)
            return;

        // https://github.com/ImGuiNET/ImGui.NET/issues/366

        List<ImGuiPlatformMonitor> monitors = [];
        for (int n = 0; n < monitors_count; n++)
        {
            glfw.GetMonitorPos(glfw_monitors[n], out var x, out var y);
            var _vid_mode = glfw.GetVideoMode(glfw_monitors[n]);
            if (_vid_mode == null)
                continue; // Failed to get Video mode (e.g. Emscripten does not support this function)
            var vid_mode = *_vid_mode;
            ImGuiPlatformMonitor monitor = default;
            monitor.MainPos = monitor.WorkPos = new((float)x, (float)y);
            monitor.MainSize = monitor.WorkSize = new(
                (float)vid_mode.Width,
                (float)vid_mode.Height
            );

            // #if GLFW_HAS_MONITOR_WORK_AREA
            //         int w, h;
            //         glfwGetMonitorWorkarea(glfw_monitors[n], &x, &y, &w, &h);
            //         if (w > 0 && h > 0) // Workaround a small GLFW issue reporting zero on monitor changes: https://github.com/glfw/glfw/pull/1761
            //         {
            //             monitor.WorkPos = ImVec2((float)x, (float)y);
            //             monitor.WorkSize = ImVec2((float)w, (float)h);
            //         }
            // #endif

            // Warning: the validity of monitor DPI information on Windows depends on the application DPI awareness settings, which generally needs to be set in the manifest or at runtime.
            glfw.GetMonitorContentScale(glfw_monitors[n], out var x_scale, out var y_scale);
            if (x_scale == 0.0f)
                continue; // Some accessibility applications are declaring virtual monitors with a DPI of 0, see #7902.
            monitor.DpiScale = x_scale;
            monitor.PlatformHandle = (void*)glfw_monitors[n]; // [...] GLFW doc states: "guaranteed to be valid only until the monitor configuration changes"
            monitors.Add(monitor);
        }

        var data = Marshal.AllocHGlobal(Marshal.SizeOf<ImGuiPlatformMonitor>() * monitors.Count);
        var pMonitors = new ImVector<ImGuiPlatformMonitor>(monitors.Count, monitors.Count, data);
        for (int i = 0; i < monitors.Count; ++i)
        {
            pMonitors[i] = monitors[i];
        }
        platform_io.NativePtr->Monitors = Unsafe.As<ImVector<ImGuiPlatformMonitor>, ImVector>(
            ref pMonitors
        );
    }

    public void NewFrame()
    {
        var io = ImGui.GetIO();

        // Setup display size (every frame to accommodate for window resizing)
        glfw.GetWindowSize(Window, out var w, out var h);
        glfw.GetFramebufferSize(Window, out var display_w, out var display_h);
        io.DisplaySize = new((float)w, (float)h);
        if (w > 0 && h > 0)
            io.DisplayFramebufferScale = new(
                (float)display_w / (float)w,
                (float)display_h / (float)h
            );
        if (WantUpdateMonitors)
            ImGui_ImplGlfw_UpdateMonitors();

        // Setup time step
        // (Accept glfwGetTime() not returning a monotonically increasing value. Seems to happens on disconnecting peripherals and probably on VMs and Emscripten, see #6491, #6189, #6114, #3644)
        var current_time = glfw.GetTime();
        if (current_time <= Time)
            current_time = Time + 0.00001f;
        io.DeltaTime = Time > 0.0 ? (float)(current_time - Time) : (float)(1.0f / 60.0f);
        Time = current_time;

        MouseIgnoreButtonUp = false;
        ImGui_ImplGlfw_UpdateMouseData();
        ImGui_ImplGlfw_UpdateMouseCursor();

        // Update game controllers (if enabled and available)
        ImGui_ImplGlfw_UpdateGamepads();
    }
}
