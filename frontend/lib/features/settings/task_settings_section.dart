import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:fluentui_system_icons/fluentui_system_icons.dart';

import '../../api/generated/models/task_concurrency.dart';
import '../../l10n/generated/app_localizations.dart';
import '../tasks/task_presentation.dart';
import 'settings_section.dart';
import 'task_settings_controller.dart';

class TaskSettingsSection extends ConsumerWidget {
  const TaskSettingsSection({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    return SettingsSection(
      icon: FluentIcons.arrow_sync_24_regular,
      title: l10n.taskSettings,
      description: l10n.taskSettingsDescription,
      value: ref.watch(taskSettingsProvider),
      onRetry: () => ref.invalidate(taskSettingsProvider),
      builder: (settings) => _TaskSettingsForm(settings: settings),
    );
  }
}

class _TaskSettingsForm extends ConsumerStatefulWidget {
  const _TaskSettingsForm({required this.settings});

  final List<TaskConcurrency> settings;

  @override
  ConsumerState<_TaskSettingsForm> createState() => _TaskSettingsFormState();
}

class _TaskSettingsFormState extends ConsumerState<_TaskSettingsForm>
    with SettingsForm<_TaskSettingsForm, List<TaskConcurrency>> {
  static const _minimum = 1;
  static const _maximum = 16;

  late final Map<TaskConcurrency, TextEditingController> _fields = {
    for (final setting in widget.settings) setting: TextEditingController(text: '${setting.concurrency}'),
  };

  @override
  List<TaskConcurrency> get saved => widget.settings;

  @override
  void fill(List<TaskConcurrency> value) {
    for (final setting in value) {
      for (final MapEntry(key: current, value: field) in _fields.entries) {
        if (current.kind == setting.kind) {
          field.text = '${setting.concurrency}';
        }
      }
    }
  }

  @override
  Future<List<TaskConcurrency>> defaults() => ref.read(taskSettingsProvider.notifier).defaults();

  @override
  Future<void> persist() => ref.read(taskSettingsProvider.notifier).save([
    for (final MapEntry(key: setting, value: field) in _fields.entries)
      setting.copyWith(concurrency: int.parse(field.text)),
  ]);

  @override
  void dispose() {
    for (final field in _fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  String? _validate(String? value, AppLocalizations l10n) {
    final number = int.tryParse(value ?? '');
    return number == null || number < _minimum || number > _maximum
        ? l10n.concurrencyInvalid(_minimum, _maximum)
        : null;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return Form(
      key: formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final MapEntry(key: setting, value: field) in _fields.entries)
            SettingsTextField(
              label: l10n.queueConcurrency(queueTitle(setting.kind, l10n)),
              required: true,
              controller: field,
              keyboardType: TextInputType.number,
              inputFormatters: [FilteringTextInputFormatter.digitsOnly],
              validator: (value) => _validate(value, l10n),
            ),
          settingsActions(),
        ],
      ),
    );
  }
}
