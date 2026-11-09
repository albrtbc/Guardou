import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/user_account.dart';
import '../../api/generated/models/user_request.dart';
import '../../app/theme/app_tokens.dart';
import '../../l10n/generated/app_localizations.dart';
import '../auth/credential_validators.dart';
import '../configuration/configuration_controller.dart';
import '../settings/settings_section.dart';
import 'quota_input.dart';
import 'user_editor_controller.dart';
import 'user_failure.dart';
import 'users_service.dart';

Future<UserAccount?> showUserEditorDialog(BuildContext context, {UserAccount? account}) => showDialog<UserAccount>(
  context: context,
  builder: (_) => _UserEditorDialog(account: account),
);

class _UserEditorDialog extends ConsumerStatefulWidget {
  const _UserEditorDialog({required this.account});

  final UserAccount? account;

  @override
  ConsumerState<_UserEditorDialog> createState() => _UserEditorDialogState();
}

class _UserEditorDialogState extends ConsumerState<_UserEditorDialog> {
  static const _width = 400.0;

  final _formKey = GlobalKey<FormState>();
  late final _username = TextEditingController(text: widget.account?.username);
  late final _displayName = TextEditingController(text: widget.account?.displayName);
  late final _email = TextEditingController(text: widget.account?.email);
  final _password = TextEditingController();
  late final _quota = TextEditingController(text: QuotaInput.format(widget.account?.quotaBytes));
  late var _isAdmin = widget.account?.isAdmin ?? false;
  late var _account = widget.account;

  bool get _isNew => widget.account == null;

  bool get _oidcLinked => _account?.externalLogins.contains(UsersService.oidcProvider) ?? false;

  @override
  void dispose() {
    _username.dispose();
    _displayName.dispose();
    _email.dispose();
    _password.dispose();
    _quota.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final request = UserRequest(
      username: _username.text,
      displayName: _displayName.text.trim(),
      email: _email.text.trim(),
      password: _password.text.isEmpty ? null : _password.text,
      isAdmin: _isAdmin,
      quotaBytes: QuotaInput.bytesOf(_quota.text),
    );
    final saved = await ref.read(userEditorControllerProvider.notifier).save(widget.account?.id, request);
    if (saved != null && mounted) {
      Navigator.of(context).pop(saved);
    }
  }

  Future<void> _removeOidcLogin(String id) async {
    final updated = await ref.read(userEditorControllerProvider.notifier).removeOidcLogin(id);
    if (updated != null && mounted) {
      setState(() => _account = updated);
    }
  }

  String? _validatePassword(AppLocalizations l10n, String? value) {
    if (value == null || value.isEmpty) {
      return _isNew ? l10n.loginValueRequired : null;
    }
    return validatePassword(l10n, ref.read(configurationControllerProvider).value?.passwordPolicy, value);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;
    final theme = Theme.of(context);
    final submission = ref.watch(userEditorControllerProvider);

    return AlertDialog(
      title: Text(_isNew ? l10n.createUser : l10n.editUser),
      scrollable: true,
      content: SizedBox(
        width: _width,
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              SettingsTextField(
                label: l10n.loginUsername,
                required: true,
                controller: _username,
                validator: (value) => validateUsername(l10n, value),
              ),
              SettingsTextField(
                label: l10n.displayName,
                required: true,
                controller: _displayName,
                validator: (value) => validateDisplayName(l10n, value),
              ),
              SettingsTextField(
                label: l10n.registerEmail,
                required: true,
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                validator: (value) => validateEmail(l10n, value),
              ),
              SettingsTextField(
                label: l10n.loginPassword,
                description: _isNew ? null : l10n.userPasswordKeep,
                required: _isNew,
                controller: _password,
                obscureText: true,
                validator: (value) => _validatePassword(l10n, value),
              ),
              SettingsTextField(
                label: l10n.userQuota,
                description: l10n.userQuotaUnlimited,
                controller: _quota,
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                validator: (value) => QuotaInput.isValid(value ?? '') ? null : l10n.userQuotaInvalid,
              ),
              SettingsField(
                label: l10n.userType,
                child: SegmentedButton<bool>(
                  segments: [
                    ButtonSegment(
                      value: false,
                      icon: const Icon(FluentIcons.person_24_regular),
                      label: Text(l10n.userTypeUser),
                    ),
                    ButtonSegment(
                      value: true,
                      icon: const Icon(FluentIcons.shield_checkmark_24_regular),
                      label: Text(l10n.accountAdministrator),
                    ),
                  ],
                  selected: {_isAdmin},
                  onSelectionChanged: (selection) => setState(() => _isAdmin = selection.single),
                ),
              ),
              if (_account case final account? when _oidcLinked) ...[
                SizedBox(height: spacing),
                Row(
                  children: [
                    Icon(FluentIcons.link_24_regular, size: 18, color: theme.colorScheme.onSurfaceVariant),
                    const SizedBox(width: 8),
                    Expanded(child: Text(l10n.userOidcLinked, style: theme.textTheme.bodyMedium)),
                    TextButton(
                      onPressed: submission.isLoading ? null : () => _removeOidcLogin(account.id),
                      child: Text(l10n.userOidcUnlink),
                    ),
                  ],
                ),
              ],
              if (submission.error case final UserFailure failure)
                Padding(
                  padding: EdgeInsets.only(top: spacing),
                  child: Text(
                    failure.message(l10n),
                    style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(l10n.cancel)),
        FilledButton(onPressed: submission.isLoading ? null : _save, child: Text(_isNew ? l10n.create : l10n.save)),
      ],
    );
  }
}
