using KidLock.App.UI;
using KidLock.Core.Input;
using KidLock.Core.Locking;
using KidLock.Core.Settings;
using static KidLock.Core.Input.VirtualKeys;

namespace KidLock.App.Tests.UI;

/// <summary>Janela + filtro real (sem ganchos): as teclas são entregues direto ao filtro.</summary>
public class HotkeySettingsFormTests
{
    [Fact]
    public void ShowsCurrentHotkey()
    {
        StaThread.Run(() =>
        {
            using var env = new FormEnvironment();
            env.Form.StartPosition = FormStartPosition.Manual;
            env.Form.Location = new Point(-20000, -20000); // fora da tela: só para desenhar
            env.Form.Show();
            Application.DoEvents();

            Assert.Equal("Ctrl + Alt + Shift + L", env.Form.LockHotkeyText);
            Assert.False(env.Form.CanSave);
            Assert.InRange(env.Form.Width, 300, 1200); // ajustou ao conteúdo
            RenderingTests.SaveForInspection(env.Form, "hotkey-window.png");
        });
    }

    [Fact]
    public void CapturedHotkey_IsShown_AndCanBeSaved()
    {
        StaThread.Run(() =>
        {
            using var env = new FormEnvironment();

            env.Form.BeginCapture(forLock: true);
            Assert.True(env.Filter.IsCapturingHotkey);
            env.Press(LControl, LMenu, 'K');

            Assert.Equal("Ctrl + Alt + K", env.Form.LockHotkeyText);
            Assert.True(env.Form.CanSave);
            Assert.False(env.Controller.IsLocked);
        });
    }

    [Fact]
    public void UnsafeHotkey_ShowsErrorAndKeepsPrevious()
    {
        StaThread.Run(() =>
        {
            using var env = new FormEnvironment();

            env.Form.BeginCapture(forLock: true);
            env.Press(LControl, 'K');

            Assert.Equal("Ctrl + Alt + Shift + L", env.Form.LockHotkeyText);
            Assert.Contains("não serve", env.Form.ErrorText, StringComparison.Ordinal);
            Assert.False(env.Form.CanSave);
        });
    }

    [Fact]
    public void ClosingDuringCapture_GivesTheKeyboardBack()
    {
        StaThread.Run(() =>
        {
            using var env = new FormEnvironment();
            env.Form.Show();
            env.Form.BeginCapture(forLock: true);

            env.Form.Close();

            Assert.False(env.Filter.IsCapturingHotkey);
            Assert.Equal(InputDecision.Pass, env.Filter.ProcessKeyboard(new KeyboardInput('A', IsKeyDown: true)).Decision);
        });
    }

    private sealed class FormEnvironment : IDisposable
    {
        public FormEnvironment()
        {
            Filter = new InputFilter(Controller, new FilterOptions());
            Form = new HotkeySettingsForm(new AppSettings(), Filter, new ImmediateSynchronizationContext(), _ => { }, () => { });
        }

        public LockController Controller { get; } = new();

        public InputFilter Filter { get; }

        public HotkeySettingsForm Form { get; }

        public void Press(params int[] keys)
        {
            foreach (var key in keys)
            {
                Filter.ProcessKeyboard(new KeyboardInput(key, IsKeyDown: true));
            }

            foreach (var key in keys.Reverse())
            {
                Filter.ProcessKeyboard(new KeyboardInput(key, IsKeyDown: false));
            }
        }

        public void Dispose() => Form.Dispose();
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }
}
