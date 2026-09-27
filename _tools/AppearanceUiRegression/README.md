# WinUI appearance regression

Run `dotnet restore _tools/AppearanceUiRegression/AppearanceUiRegression.csproj -p:Platform=x64` once, then run `./_tools/AppearanceUiRegression/Run.ps1` on Windows.

Uses a real WinUI window, compiled `x:Bind`, the production playback-rate ViewModel partial, the production Composition/Win2D background control, and real Toolkit SettingsExpander templates. The background settings layout is extracted directly from the shipping XAML; bindings unrelated to item realization are replaced with inert values. The host runs outside the visible desktop and exits after writing `result.txt` beside the executable. DSP dependencies outside the tested binding are substituted; the complete DSP page, picker dialog, packaged app, and system high-contrast switching still need manual checks.

Checks background settings expansion, collapse/re-expansion and error-message visibility; default/saved rate initialization, transient selection protection, all nine rate selections, legacy custom rates, real image decoding/blur, rapid source replacement, missing and corrupt image fallback, clear, unload/reload, and repeated disposal. The settings serialization regression separately checks backward-compatible defaults and path/blur persistence.
