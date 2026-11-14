import {Center} from '@astryxdesign/core/Center';
import {Layout, LayoutContent} from '@astryxdesign/core/Layout';
import {Spinner} from '@astryxdesign/core/Spinner';
import {StackItem, VStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {PageHeader, type PageHeaderProps} from './PageHeader';

export interface PageProps extends PageHeaderProps {
  /** Shows a spinner instead of the content, e.g. while the page's data loads. */
  isLoading?: boolean;
  loadingMessage?: string;
  /**
   * Caps the content width (px), centred. Every page uses the same width by default, so titles and cards line up
   * from page to page.
   */
  maxWidth?: number;
  /**
   * Makes the content fill the window's height below the header, e.g. so a list's table reaches the bottom of
   * the page however few rows it has. The content gets the remaining height to fill (`height="100%"`).
   */
  fill?: boolean;
  /** Dialogs the page opens. They render outside the content, so they stay available while it loads. */
  dialogs?: ReactNode;
  children?: ReactNode;
}

/**
 * The frame of every signed-in page (replaces Blazor's Page): header, then content or a loading spinner.
 * Auth checks and data loading live in the route (guard and queries), not here.
 */
/** The content width every page shares. */
export const PAGE_MAX_WIDTH = 1600;

/**
 * Below this width, pages with two side-by-side columns (Home, a procedure) don't fit them, and show one at a
 * time or stack them instead.
 */
export const NARROW_WINDOW_QUERY = '(max-width: 1399px)';

export function Page({
  isLoading = false,
  loadingMessage = 'Loading...',
  maxWidth = PAGE_MAX_WIDTH,
  fill = false,
  dialogs,
  children,
  ...header
}: PageProps) {
  return (
    // A Layout sits flush in AppShell's content area and insets only its regions, so the page goes in a
    // LayoutContent region with the same 24px inset as pages without a Layout.
    <Layout height={fill ? 'fill' : 'auto'} padding={6} contentWidth={maxWidth}>
      <LayoutContent>
        <VStack gap={6} height={fill ? '100%' : undefined}>
          <PageHeader {...header} />
          {isLoading ? (
            <Center axis="horizontal" padding={10}>
              <Spinner size="lg" label={loadingMessage} />
            </Center>
          ) : fill ? (
            <StackItem size="fill">{children}</StackItem>
          ) : (
            children
          )}
        </VStack>
        {dialogs}
      </LayoutContent>
    </Layout>
  );
}
