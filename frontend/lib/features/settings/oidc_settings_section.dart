import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/oidc_settings_change.dart';
import '../../api/generated/models/oidc_settings_view.dart';
import '../../l10n/generated/app_localizations.dart';
import 'oidc_settings_controller.dart';
import 'settings_copy_field.dart';
import 'settings_section.dart';

class OidcSettingsSection extends ConsumerWidget {
  const OidcSettingsSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return SettingsSection(
      icon: FluentIcons.key_24_regular,
      title: l10n.oidcSettings,
      description: l10n.oidcSettingsDescription,
      value: ref.watch(oidcSettingsProvider),
      onRetry: () => ref.invalidate(oidcSettingsProvider),
      builder: (settings) => _OidcSettingsForm(settings: settings),
    );
  }
}

class _OidcSettingsForm extends ConsumerStatefulWidget {
  const _OidcSettingsForm({required this.settings});

  final OidcSettingsView settings;

  @override
  ConsumerState<_OidcSettingsForm> createState() => _OidcSettingsFormState();
}

class _OidcSettingsFormState extends ConsumerState<_OidcSettingsForm>
    with SettingsForm<_OidcSettingsForm, OidcSettingsView> {
  static final _whitespace = RegExp(r'\s+');

  final _issuer = TextEditingController();
  final _clientId = TextEditingController();
  final _clientSecret = TextEditingController();
  final _scopes = TextEditingController();
  final _userClaim = TextEditingController();
  var _autoCreateUsers = true;
  var _passwordLogin = true;

  bool get _configured => _issuer.text.trim().isNotEmpty && _clientId.text.trim().isNotEmpty;

  @override
  OidcSettingsView get saved => widget.settings;

  @override
  void initState() {
    super.initState();
    fill(widget.settings);
  }

  @override
  void fill(OidcSettingsView value) {
    _issuer.text = value.issuer ?? '';
    _clientId.text = value.clientId ?? '';
    _clientSecret.clear();
    _scopes.text = value.scopes.join(' ');
    _userClaim.text = value.userClaim;
    _autoCreateUsers = value.autoCreateUsers;
    _passwordLogin = value.passwordLogin;
  }

  @override
  Future<OidcSettingsView> defaults() => ref.read(oidcSettingsProvider.notifier).defaults();

  @override
  Future<void> persist() => ref
      .read(oidcSettingsProvider.notifier)
      .save(
        OidcSettingsChange(
          issuer: trimmedOrNull(_issuer.text),
          clientId: trimmedOrNull(_clientId.text),
          clientSecret: _clientSecret.text.isEmpty ? null : _clientSecret.text,
          scopes: _scopes.text.split(_whitespace).where((scope) => scope.isNotEmpty).toList(),
          userClaim: _userClaim.text.trim(),
          autoCreateUsers: _autoCreateUsers,
          passwordLogin: _passwordLogin || !_configured,
        ),
      );

  @override
  void dispose() {
    for (final controller in [_issuer, _clientId, _clientSecret, _scopes, _userClaim]) {
      controller.dispose();
    }
    super.dispose();
  }

  String? _validateClient(AppLocalizations l10n) =>
      _issuer.text.trim().isEmpty == _clientId.text.trim().isEmpty ? null : l10n.oidcClientRequired;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    String? locked(String field) => lockedVariable(widget.settings.locked, field);

    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SettingsTextField(
            label: l10n.oidcIssuer,
            controller: _issuer,
            keyboardType: TextInputType.url,
            lockedBy: locked('issuer'),
            onChanged: (_) => setState(() {}),
            validator: (value) => validateHttpsUrl(l10n, value) ?? _validateClient(l10n),
          ),
          SettingsTextField(
            label: l10n.oidcClientId,
            controller: _clientId,
            lockedBy: locked('clientId'),
            onChanged: (_) => setState(() {}),
            validator: (_) => _validateClient(l10n),
          ),
          SettingsTextField(
            label: l10n.oidcClientSecret,
            controller: _clientSecret,
            obscureText: true,
            lockedBy: locked('clientSecret'),
          ),
          SettingsTextField(label: l10n.oidcScopes, controller: _scopes, lockedBy: locked('scopes')),
          SettingsTextField(
            label: l10n.oidcUserClaim,
            required: true,
            controller: _userClaim,
            lockedBy: locked('userClaim'),
            validator: (value) => (value?.trim() ?? '').isEmpty ? l10n.loginValueRequired : null,
          ),
          SettingsSwitch(
            label: l10n.oidcAutoCreateUsers,
            value: _autoCreateUsers,
            onChanged: (value) => setState(() => _autoCreateUsers = value),
          ),
          SettingsSwitch(
            label: l10n.oidcPasswordLogin,
            value: _passwordLogin || !_configured,
            onChanged: _configured ? (value) => setState(() => _passwordLogin = value) : null,
          ),
          SettingsCopyField(label: l10n.oidcRedirectUri, value: widget.settings.redirectUri),
          SettingsCopyField(label: l10n.oidcPostLogoutRedirectUri, value: widget.settings.postLogoutRedirectUri),
          settingsActions(),
        ],
      ),
    );
  }
}
