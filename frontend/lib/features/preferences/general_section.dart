import 'package:fluentui_system_icons/fluentui_system_icons.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../api/generated/models/language.dart';
import '../../api/generated/models/start_page.dart';
import '../../api/generated/models/theme_preference.dart';
import '../../api/generated/models/user_preferences.dart';
import '../../l10n/generated/app_localizations.dart';
import '../settings/settings_section.dart';
import 'preference_choices.dart';
import 'preferences_section.dart';

class GeneralSection extends StatelessWidget {
  const GeneralSection({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return PreferencesSection(
      icon: FluentIcons.settings_24_regular,
      title: l10n.generalSettings,
      description: l10n.generalSettingsDescription,
      builder: (preferences) => _GeneralForm(preferences: preferences),
    );
  }
}

class _GeneralForm extends ConsumerStatefulWidget {
  const _GeneralForm({required this.preferences});

  final UserPreferences preferences;

  @override
  ConsumerState<_GeneralForm> createState() => _GeneralFormState();
}

class _GeneralFormState extends ConsumerState<_GeneralForm>
    with SettingsForm<_GeneralForm, UserPreferences>, PreferencesForm<_GeneralForm> {
  late ThemePreference _theme;
  late Language _language;
  late StartPage _startPage;

  @override
  UserPreferences get saved => widget.preferences;

  @override
  void initState() {
    super.initState();
    fill(widget.preferences);
  }

  @override
  void fill(UserPreferences value) {
    _theme = value.theme;
    _language = value.language;
    _startPage = value.startPage;
  }

  @override
  UserPreferences changed(UserPreferences current) =>
      current.copyWith(theme: _theme, language: _language, startPage: _startPage);

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SettingsChoiceField(
            label: l10n.theme,
            value: _theme,
            choices: ThemePreference.$valuesDefined,
            labelOf: (choice) => choice.label(l10n),
            onChanged: (choice) => setState(() => _theme = choice),
          ),
          SettingsChoiceField(
            label: l10n.language,
            value: _language,
            choices: Language.$valuesDefined,
            labelOf: (choice) => choice.label(l10n),
            onChanged: (choice) => setState(() => _language = choice),
          ),
          SettingsChoiceField(
            label: l10n.startPage,
            value: _startPage,
            choices: StartPage.$valuesDefined,
            labelOf: (choice) => choice.label(l10n),
            onChanged: (choice) => setState(() => _startPage = choice),
          ),
          settingsActions(),
        ],
      ),
    );
  }
}
