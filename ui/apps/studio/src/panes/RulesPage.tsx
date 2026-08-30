import type { RuleListEntry, RulesApiClient } from '@motiv-rules/core';
import type { Page } from '../routing/useHashRoute.js';
import { RuleHeader } from './RuleHeader.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { CheckoutPane } from './CheckoutPane.js';

/** The rules page: today's shell, unchanged, now behind a route. */
export function RulesPage(props: {
  client: RulesApiClient;
  page: Page;
  onLoaded?: (entry: RuleListEntry | null) => void;
}) {
  return (
    <>
      <RuleHeader
        client={props.client}
        page={props.page}
        {...(props.onLoaded ? { onLoaded: props.onLoaded } : {})}
      />
      {/*
        Each pane below fetches GET /catalog on mount (EditorPane and EvaluatePane
        via useCatalog, CheckoutPane directly) — and EditorPane's builder surface
        fetches once more of its own, so up to four requests for the same static
        payload. Deduping would mean lifting the catalog here and passing it down,
        but each pane's self-contained wiring is a deliberate seam Studio exists
        to show, so the duplicate requests are accepted.
      */}
      <div className="shell-body">
        <EditorPane client={props.client} />
        <EvaluatePane client={props.client} />
      </div>
      <CheckoutPane client={props.client} />
    </>
  );
}
