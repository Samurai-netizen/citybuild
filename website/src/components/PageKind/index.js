import React from 'react';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import {useDoc} from '@docusaurus/plugin-content-docs/client';
import {pageKind, repoPath} from './kinds';
import styles from './styles.module.css';

/** One line above every page: what kind of page it is, and the file it comes from. */
export default function PageKind() {
  const {metadata} = useDoc();
  const {siteConfig} = useDocusaurusContext();
  const path = repoPath(metadata.source);
  const {kind, label, text, isProtected} = pageKind(path);
  const {githubRepoUrl, branch} = siteConfig.customFields;
  return (
    <div className={`${styles.kind} ${styles[kind]}`} role="note">
      <span className={styles.label}>{label}</span>
      <span className={styles.text}>
        {text}
        {isProtected && ' Protected: every edit needs the owner’s approval.'}
      </span>
      <a className={styles.source} href={`${githubRepoUrl}/blob/${branch}/${path}`}>
        <code>{path}</code>
      </a>
    </div>
  );
}
