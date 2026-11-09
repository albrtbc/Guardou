import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/upload_conflict_policy.dart';
import '../../api/generated/models/user_preferences.dart';
import '../../l10n/generated/app_localizations.dart';
import '../settings/settings_section.dart';
import 'preference_choices.dart';
import 'preferences_section.dart';

class UploadSection extends StatelessWidget {
  const UploadSection({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return PreferencesSection(
      icon: FluentIcons.arrow_upload_24_regular,
      title: l10n.uploadSettings,
      description: l10n.uploadSettingsDescription,
      builder: (preferences) => _UploadForm(preferences: preferences),
    );
  }
}

class _UploadForm extends ConsumerStatefulWidget {
  const _UploadForm({required this.preferences});

  final UserPreferences preferences;

  @override
  ConsumerState<_UploadForm> createState() => _UploadFormState();
}

class _UploadFormState extends ConsumerState<_UploadForm>
    with SettingsForm<_UploadForm, UserPreferences>, PreferencesForm<_UploadForm> {
  late UploadConflictPolicy _conflictPolicy;

  @override
  UserPreferences get saved => widget.preferences;

  @override
  void initState() {
    super.initState();
    fill(widget.preferences);
  }

  @override
  void fill(UserPreferences value) => _conflictPolicy = value.uploadConflictPolicy;

  @override
  UserPreferences changed(UserPreferences current) => current.copyWith(uploadConflictPolicy: _conflictPolicy);

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SettingsChoiceField(
            label: l10n.uploadConflictPolicy,
            description: l10n.uploadConflictPolicyDescription,
            value: _conflictPolicy,
            choices: UploadConflictPolicy.$valuesDefined,
            labelOf: (choice) => choice.label(l10n),
            onChanged: (choice) => setState(() => _conflictPolicy = choice),
          ),
          settingsActions(),
        ],
      ),
    );
  }
}
