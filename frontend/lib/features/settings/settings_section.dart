import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/locked_setting.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/api/api_problem.dart';
import '../../l10n/generated/app_localizations.dart';

class SettingsSection<T> extends StatelessWidget {
  const SettingsSection({
    super.key,
    required this.icon,
    required this.title,
    required this.description,
    required this.value,
    required this.onRetry,
    required this.builder,
  });

  final IconData icon;
  final String title;
  final String description;
  final AsyncValue<T> value;
  final VoidCallback onRetry;
  final Widget Function(T value) builder;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final tokens = AppTokens.of(context);
    final theme = Theme.of(context);
    final colors = theme.colorScheme;

    return Material(
      color: colors.surfaceContainer,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(tokens.panelRadius),
        side: BorderSide(color: colors.outlineVariant),
      ),
      child: ExpansionTile(
        maintainState: true,
        shape: const Border(),
        collapsedShape: const Border(),
        tilePadding: EdgeInsets.symmetric(horizontal: tokens.spacing * 1.25, vertical: tokens.spacing / 2),
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: colors.primary, size: 22),
                const SizedBox(width: 12),
                Flexible(
                  child: Text(title, style: theme.textTheme.titleMedium?.copyWith(color: colors.primary)),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(description, style: theme.textTheme.bodyMedium?.copyWith(color: colors.onSurfaceVariant)),
          ],
        ),
        childrenPadding: EdgeInsets.fromLTRB(tokens.spacing * 2, 0, tokens.spacing * 2, tokens.spacing * 1.5),
        children: [
          switch (value) {
            AsyncValue<T>(:final value?) => builder(value),
            AsyncValue(error: _?) => Column(
              children: [
                Text(l10n.settingsLoadErrorTitle),
                TextButton(onPressed: onRetry, child: Text(l10n.errorRetry)),
              ],
            ),
            _ => Padding(
              padding: EdgeInsets.all(tokens.spacing),
              child: Center(child: CircularProgressIndicator(semanticsLabel: l10n.loading)),
            ),
          },
        ],
      ),
    );
  }
}

mixin SettingsForm<W extends ConsumerStatefulWidget, T> on ConsumerState<W> {
  final formKey = GlobalKey<FormState>();
  bool _saving = false;

  T get saved;

  void fill(T value);

  Future<T> defaults();

  Future<void> persist();

  void _reset() => setState(() => fill(saved));

  Future<void> _restoreDefaults() async {
    final messenger = ScaffoldMessenger.of(context);
    final error = AppLocalizations.of(context).genericError;
    try {
      final value = await defaults();
      if (mounted) {
        setState(() => fill(value));
      }
    } on DioException {
      messenger.showSnackBar(SnackBar(content: Text(error)));
    }
  }

  Future<void> _save() async {
    if (!(formKey.currentState?.validate() ?? false)) {
      return;
    }
    final l10n = AppLocalizations.of(context);
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _saving = true);
    try {
      await persist();
      messenger.showSnackBar(SnackBar(content: Text(l10n.settingsSaved)));
    } on DioException catch (error) {
      final invalid = switch (error.error) {
        ApiProblem(status: 400) => true,
        _ => false,
      };
      messenger.showSnackBar(SnackBar(content: Text(invalid ? l10n.settingsInvalid : l10n.genericError)));
    } finally {
      if (mounted) {
        setState(() => _saving = false);
      }
    }
  }

  Widget settingsActions() => _SettingsActions(
    saving: _saving,
    onRestoreDefaults: () => unawaited(_restoreDefaults()),
    onReset: _reset,
    onSave: () => unawaited(_save()),
  );
}

class _SettingsActions extends StatelessWidget {
  const _SettingsActions({
    required this.saving,
    required this.onRestoreDefaults,
    required this.onReset,
    required this.onSave,
  });

  final bool saving;
  final VoidCallback onRestoreDefaults;
  final VoidCallback onReset;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final spacing = AppTokens.of(context).spacing;

    return Padding(
      padding: EdgeInsets.only(top: spacing * 1.5),
      child: Wrap(
        alignment: WrapAlignment.spaceBetween,
        crossAxisAlignment: WrapCrossAlignment.center,
        spacing: spacing / 2,
        runSpacing: spacing / 2,
        children: [
          TextButton(onPressed: saving ? null : onRestoreDefaults, child: Text(l10n.settingsRestoreDefaults)),
          Wrap(
            spacing: spacing / 2,
            children: [
              FilledButton.tonal(onPressed: saving ? null : onReset, child: Text(l10n.settingsReset)),
              FilledButton(onPressed: saving ? null : onSave, child: Text(l10n.save)),
            ],
          ),
        ],
      ),
    );
  }
}

class SettingsField extends StatelessWidget {
  const SettingsField({super.key, required this.label, this.description, this.required = false, required this.child});

  final String label;
  final String? description;
  final bool required;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final text = description;

    return Padding(
      padding: EdgeInsets.only(top: AppTokens.of(context).spacing * 1.25),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text.rich(
            TextSpan(
              text: label,
              children: [
                if (required)
                  TextSpan(
                    text: ' *',
                    style: TextStyle(color: colors.error),
                  ),
              ],
            ),
            style: theme.textTheme.titleSmall?.copyWith(color: colors.primary),
          ),
          if (text != null) ...[const SizedBox(height: 4), Text(text, style: theme.textTheme.bodyMedium)],
          const SizedBox(height: 8),
          child,
        ],
      ),
    );
  }
}

class SettingsTextField extends StatelessWidget {
  const SettingsTextField({
    super.key,
    required this.label,
    this.description,
    this.required = false,
    required this.controller,
    this.validator,
    this.onChanged,
    this.keyboardType,
    this.obscureText = false,
    this.inputFormatters,
    this.lockedBy,
  });

  final String label;
  final String? description;
  final bool required;
  final TextEditingController controller;
  final FormFieldValidator<String>? validator;
  final ValueChanged<String>? onChanged;
  final TextInputType? keyboardType;
  final bool obscureText;
  final List<TextInputFormatter>? inputFormatters;
  final String? lockedBy;

  @override
  Widget build(BuildContext context) {
    final variable = lockedBy;
    final reason = variable == null ? null : AppLocalizations.of(context).settingsLocked(variable);

    return SettingsField(
      label: label,
      description: description,
      required: required,
      child: TextFormField(
        controller: controller,
        readOnly: reason != null,
        keyboardType: keyboardType,
        obscureText: obscureText,
        inputFormatters: inputFormatters,
        autofillHints: obscureText ? const [] : null,
        decoration: settingsInputDecoration(context).copyWith(
          helperText: reason,
          helperMaxLines: 3,
          suffixIcon: reason == null
              ? null
              : Tooltip(message: reason, child: const Icon(FluentIcons.lock_closed_24_regular, size: 18)),
        ),
        onChanged: onChanged,
        validator: validator,
      ),
    );
  }
}

String? lockedVariable(List<LockedSetting> locked, String field) =>
    locked.where((setting) => setting.field == field).firstOrNull?.variable;

InputDecoration settingsInputDecoration(BuildContext context) {
  final colors = Theme.of(context).colorScheme;
  final shape = OutlineInputBorder(borderRadius: BorderRadius.circular(8), borderSide: BorderSide.none);
  return InputDecoration(
    filled: true,
    fillColor: colors.surfaceContainerHighest,
    border: shape,
    enabledBorder: shape,
    focusedBorder: shape.copyWith(borderSide: BorderSide(color: colors.primary, width: 2)),
    errorBorder: shape.copyWith(borderSide: BorderSide(color: colors.error)),
    focusedErrorBorder: shape.copyWith(borderSide: BorderSide(color: colors.error, width: 2)),
    errorMaxLines: 3,
  );
}

class SettingsChoiceField<T> extends StatelessWidget {
  const SettingsChoiceField({
    super.key,
    required this.label,
    this.description,
    required this.value,
    required this.choices,
    required this.labelOf,
    required this.onChanged,
  });

  final String label;
  final String? description;
  final T value;
  final List<T> choices;
  final String Function(T choice) labelOf;
  final ValueChanged<T> onChanged;

  @override
  Widget build(BuildContext context) => SettingsField(
    label: label,
    description: description,
    child: InputDecorator(
      decoration: settingsInputDecoration(context).copyWith(contentPadding: const EdgeInsets.symmetric(horizontal: 12)),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<T>(
          value: value,
          isExpanded: true,
          items: [
            for (final choice in choices)
              DropdownMenuItem(
                value: choice,
                child: Text(labelOf(choice), overflow: TextOverflow.ellipsis),
              ),
          ],
          onChanged: (choice) {
            if (choice != null) {
              onChanged(choice);
            }
          },
        ),
      ),
    ),
  );
}

class SettingsSwitch extends StatelessWidget {
  const SettingsSwitch({super.key, required this.label, required this.value, required this.onChanged});

  final String label;
  final bool value;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: EdgeInsets.only(top: AppTokens.of(context).spacing * 1.25),
      child: MergeSemantics(
        child: Row(
          children: [
            Expanded(
              child: Text(label, style: theme.textTheme.titleSmall?.copyWith(color: theme.colorScheme.primary)),
            ),
            const SizedBox(width: 16),
            Switch(value: value, onChanged: onChanged),
          ],
        ),
      ),
    );
  }
}

String? validateHttpUrl(AppLocalizations l10n, String? value) =>
    _validateUrl(value, const {'http', 'https'}, l10n.settingsUrlInvalid);

String? validateHttpsUrl(AppLocalizations l10n, String? value) =>
    _validateUrl(value, const {'https'}, l10n.settingsHttpsUrlInvalid);

String? _validateUrl(String? value, Set<String> schemes, String error) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return null;
  }
  final uri = Uri.tryParse(text);
  final valid = uri != null && uri.hasAuthority && schemes.contains(uri.scheme);
  return valid ? null : error;
}

String? trimmedOrNull(String value) {
  final text = value.trim();
  return text.isEmpty ? null : text;
}
