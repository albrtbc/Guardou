import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/browser/browser_files.dart';
import 'file_actions.dart';
import 'item_drag.dart';

class DropTargetRegistration {
  DropTargetRegistration(this.key, this.onDrop);

  final GlobalKey key;
  final ValueChanged<List<LocalFile>> onDrop;

  Rect? get bounds {
    final box = key.currentContext?.findRenderObject();
    if (box is! RenderBox || !box.attached || !box.hasSize) {
      return null;
    }
    return box.localToGlobal(Offset.zero) & box.size;
  }
}

final fileDropControllerProvider = NotifierProvider<FileDropController, DropTargetRegistration?>(
  FileDropController.new,
);

class FileDropController extends Notifier<DropTargetRegistration?> {
  final _targets = <DropTargetRegistration>{};

  @override
  DropTargetRegistration? build() => null;

  void register(DropTargetRegistration target) => _targets.add(target);

  void unregister(DropTargetRegistration target) {
    _targets.remove(target);
    if (ref.mounted && state == target) {
      unawaited(Future.microtask(_clearHover));
    }
  }

  void _clearHover() {
    if (ref.mounted) {
      state = null;
    }
  }

  void handle(FileDragEvent event) {
    switch (event) {
      case FileDragOver(:final position):
        state = _targetAt(position);
      case FileDragLeave():
        state = null;
      case FileDrop(:final position, :final files):
        final target = _targetAt(position);
        state = null;
        if (target != null) {
          unawaited(files.then(target.onDrop));
        }
    }
  }

  DropTargetRegistration? _targetAt(Offset position) {
    DropTargetRegistration? best;
    double? bestArea;
    for (final target in _targets) {
      final bounds = target.bounds;
      if (bounds == null || !bounds.contains(position)) {
        continue;
      }
      final area = bounds.width * bounds.height;
      if (bestArea == null || area < bestArea) {
        best = target;
        bestArea = area;
      }
    }
    return best;
  }
}

class FileDropListener extends ConsumerStatefulWidget {
  const FileDropListener({super.key, required this.child});

  final Widget child;

  @override
  ConsumerState<FileDropListener> createState() => _FileDropListenerState();
}

class _FileDropListenerState extends ConsumerState<FileDropListener> {
  StreamSubscription<FileDragEvent>? _subscription;

  @override
  void initState() {
    super.initState();
    final controller = ref.read(fileDropControllerProvider.notifier);
    _subscription = ref.read(browserFilesProvider).dragEvents.listen(controller.handle);
  }

  @override
  void dispose() {
    unawaited(_subscription?.cancel());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}

class FileDropTarget extends ConsumerStatefulWidget {
  const FileDropTarget({super.key, required this.folderId, required this.enabled, required this.child});

  final String folderId;
  final bool enabled;
  final Widget child;

  @override
  ConsumerState<FileDropTarget> createState() => _FileDropTargetState();
}

class _FileDropTargetState extends ConsumerState<FileDropTarget> {
  final _key = GlobalKey();
  late final FileDropController _controller;
  late final _registration = DropTargetRegistration(_key, _drop);

  void _drop(List<LocalFile> files) {
    if (mounted) {
      FileActions(context).upload(widget.folderId, files);
    }
  }

  @override
  void initState() {
    super.initState();
    _controller = ref.read(fileDropControllerProvider.notifier);
    if (widget.enabled) {
      _controller.register(_registration);
    }
  }

  @override
  void didUpdateWidget(FileDropTarget oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.enabled != oldWidget.enabled) {
      widget.enabled ? _controller.register(_registration) : _controller.unregister(_registration);
    }
  }

  @override
  void dispose() {
    _controller.unregister(_registration);
    super.dispose();
  }

  bool _accepts(DraggedItems items) => widget.enabled && items.canMoveTo(widget.folderId);

  void _move(DraggedItems items) => unawaited(FileActions(context).move(items.entries, widget.folderId));

  @override
  Widget build(BuildContext context) {
    final filesHovered = ref.watch(fileDropControllerProvider.select((target) => target == _registration));
    return DragTarget<DraggedItems>(
      onWillAcceptWithDetails: (details) => _accepts(details.data),
      onAcceptWithDetails: (details) => _move(details.data),
      builder: (context, candidates, _) => _highlight(context, hovered: filesHovered || candidates.isNotEmpty),
    );
  }

  Widget _highlight(BuildContext context, {required bool hovered}) {
    final colorScheme = Theme.of(context).colorScheme;
    return DecoratedBox(
      key: _key,
      position: DecorationPosition.foreground,
      decoration: BoxDecoration(
        color: hovered ? colorScheme.primary.withValues(alpha: 0.08) : null,
        border: hovered ? Border.all(color: colorScheme.primary, width: 2) : null,
        borderRadius: BorderRadius.circular(8),
      ),
      child: widget.child,
    );
  }
}
