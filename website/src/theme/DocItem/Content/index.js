// Wraps the theme's page content (swizzle --wrap) to put the page-kind line above every page.
import React from 'react';
import Content from '@theme-original/DocItem/Content';
import PageKind from '@site/src/components/PageKind';

// The page content with the page-kind line above it.
export default function ContentWrapper(props) {
  return (
    <>
      <PageKind />
      <Content {...props} />
    </>
  );
}
