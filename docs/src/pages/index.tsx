import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Layout from '@theme/Layout';
import ThemedImage from '@theme/ThemedImage';
import HomepageFeatures from '@site/src/components/HomepageFeatures';
import Heading from '@theme/Heading';

import styles from './index.module.css';

function HomepageHeader() {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero', styles.heroBanner)}>
      <div className="container">
        <Heading as="h1" className="hero__title">
          {siteConfig.title}
        </Heading>
        <p className="hero__subtitle">{siteConfig.tagline}</p>
        <div className={styles.buttons}>
          <Link
            className="button button--primary button--lg"
            to="/docs/overview/quick-start">
            Get Started
          </Link>
        </div>
        <ThemedImage
          className={styles.screenshot}
          sources={{
            light: useBaseUrl('/img/screenshot-light.webp'),
            dark: useBaseUrl('/img/screenshot-dark.webp'),
          }}
          width={2234}
          height={1416}
          alt="Guardou running on desktop and mobile"
        />
      </div>
    </header>
  );
}

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout
      title={siteConfig.tagline}
      description="Guardou is a free, open source, self-hosted file storage solution with a simple web UI.">
      <HomepageHeader />
      <main>
        <HomepageFeatures />
      </main>
    </Layout>
  );
}
