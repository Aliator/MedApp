import {useTooltip} from '@astryxdesign/core/Tooltip';
import {useCallback, useLayoutEffect, useRef, type ReactNode} from 'react';

export interface DisabledReasonProps {
  reason: string | undefined;
  children: ReactNode;
}

export function DisabledReason({reason, children}: DisabledReasonProps) {
  const hasReason = Boolean(reason);
  const tooltip = useTooltip({touchTrigger: 'tap', isEnabled: hasReason});
  const wrapperRef = useRef<HTMLSpanElement>(null);
  const isTouchPress = useRef(false);
  const {describedBy, ref: attach, show} = tooltip;
  const setWrapper = useCallback(
    (el: HTMLSpanElement | null) => {
      wrapperRef.current = el;
      attach(el);
    },
    [attach],
  );

  useLayoutEffect(() => {
    const control = wrapperRef.current?.firstElementChild;
    if (!control || !hasReason) return;
    const existing = control.getAttribute('aria-describedby');
    control.setAttribute('aria-describedby', [existing, describedBy].filter(Boolean).join(' '));
    return () => {
      if (existing) control.setAttribute('aria-describedby', existing);
      else control.removeAttribute('aria-describedby');
    };
  }, [describedBy, hasReason]);

  return (
    <span
      ref={setWrapper}
      style={{display: 'inline-flex'}}
      onPointerDownCapture={(event) => {
        isTouchPress.current = event.pointerType === 'touch' || event.pointerType === 'pen';
      }}
      onClickCapture={() => {
        if (hasReason && !isTouchPress.current) show();
      }}>
      {children}
      {hasReason && tooltip.renderTooltip(reason)}
    </span>
  );
}
