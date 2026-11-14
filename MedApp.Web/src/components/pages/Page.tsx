import {Center} from '@astryxdesign/core/Center';
import {Layout, LayoutContent} from '@astryxdesign/core/Layout';
import {Spinner} from '@astryxdesign/core/Spinner';
import {VStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {PageHeader, type PageHeaderProps} from './PageHeader';

export interface PageProps extends PageHeaderProps {
  /** Shows a spinner instead of the content, e.g. while the page's data loads. */
  isLoading?: boolean;
  loadingMessage?: string;
  /** Caps the content width (px), centred; forms use this. */
  maxWidth?: number;
  /** Dialogs the page opens. They render outside the content, so they stay available while it loads. */
  dialogs?: ReactNode;
  children?: ReactNode;
}

/**
 * The frame of every signed-in page (replaces Blazor's Page): header, then content or a loading spinner.
 * Auth checks and data loading live in the route (guard and queries), not here.
 */
export function Page({isLoading = false, loadingMessage = 'Loading...', maxWidth, dialogs, children, ...header}: PageProps) {
  return (
    // A Layout sits flush in AppShell's content area and insets only its regions, so the page goes in a
    // LayoutContent region with the same 24px inset as pages without a Layout.
    <Layout height="auto" padding={6} contentWidth={maxWidth}>
      <LayoutContent>
        <VStack gap={6}>
          <PageHeader {...header} />
          {isLoading ? (
            <Center axis="horizontal" padding={10}>
              <Spinner size="lg" label={loadingMessage} />
            </Center>
          ) : (
            children
          )}
        </VStack>
        {dialogs}
      </LayoutContent>
    </Layout>
  );
}
