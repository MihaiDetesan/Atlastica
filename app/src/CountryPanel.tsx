import { useEffect, useState } from "react";
import { fetchCountryFacts } from "./data";
import type { CountryFacts } from "./types";

interface CountryPanelProps {
  isoCode: string;
  name: string;
  onClose: () => void;
}

function formatNumber(n: number | null): string {
  return n === null ? "unknown" : n.toLocaleString();
}

export function CountryPanel({ isoCode, name, onClose }: CountryPanelProps) {
  const [facts, setFacts] = useState<CountryFacts | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchCountryFacts(isoCode)
      .then(setFacts)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : String(err)));
    // isoCode changes remount this component (parent renders key={isoCode}),
    // so state always starts fresh - no manual reset needed here.
  }, [isoCode]);

  return (
    <aside
      style={{
        position: "absolute",
        top: 12,
        right: 12,
        width: 320,
        maxHeight: "calc(100vh - 24px)",
        overflowY: "auto",
        background: "rgba(20, 20, 20, 0.92)",
        color: "#fff",
        padding: 16,
        borderRadius: 8,
        fontFamily: "system-ui, sans-serif",
      }}
    >
      <button
        type="button"
        onClick={onClose}
        style={{ float: "right", background: "none", border: "none", color: "#fff", fontSize: 16, cursor: "pointer" }}
        aria-label="Close"
      >
        ×
      </button>
      <h2 style={{ margin: "0 0 4px", fontSize: 18, display: "flex", alignItems: "center", gap: 8 }}>
        <img
          src={`https://flagcdn.com/24x18/${isoCode.toLowerCase()}.png`}
          srcSet={`https://flagcdn.com/48x36/${isoCode.toLowerCase()}.png 2x`}
          alt=""
          width={24}
          height={18}
          style={{ borderRadius: 2, flexShrink: 0 }}
        />
        {name}
      </h2>
      <p style={{ margin: "0 0 12px", opacity: 0.7, fontSize: 13 }}>{isoCode}</p>

      {error && <p style={{ color: "#e34948" }}>Failed to load facts: {error}</p>}

      {!facts && !error && <p style={{ opacity: 0.8, fontSize: 13 }}>Loading facts...</p>}

      {facts && (
        <dl style={{ fontSize: 13, lineHeight: 1.6, margin: 0 }}>
          <Field label="Capital" value={facts.capital ?? "unknown"} />
          <Field label="Population" value={formatNumber(facts.population)} />
          <Field label="Government" value={facts.governmentForm ?? "unknown"} />
          <Field
            label="Official languages"
            value={facts.officialLanguages.length > 0 ? facts.officialLanguages.join(", ") : "unknown"}
          />
          <Field label="Area" value={facts.areaKm2 !== null ? `${formatNumber(facts.areaKm2)} km²` : "unknown"} />
          <Field label="Currency" value={facts.currency ?? "unknown"} />
          {/* These two are opportunistic (only well-known/distinctive facts get
              extracted at all) - omit the row entirely rather than show a
              placeholder when there's nothing notable to report. */}
          {facts.notableGeographicFeatures && <Field label="Notable geography" value={facts.notableGeographicFeatures} />}
          {facts.biodiversityFacts && <Field label="Biodiversity" value={facts.biodiversityFacts} />}
          <p style={{ opacity: 0.5, fontSize: 11, marginTop: 12 }}>
            Facts from Wikidata &amp; Wikipedia (CC BY-SA), last refreshed{" "}
            {new Date(facts.fetchedAt).toLocaleDateString()}.
          </p>
        </dl>
      )}
    </aside>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ marginBottom: 6 }}>
      <dt style={{ opacity: 0.6, display: "inline" }}>{label}: </dt>
      <dd style={{ display: "inline", margin: 0 }}>{value}</dd>
    </div>
  );
}
