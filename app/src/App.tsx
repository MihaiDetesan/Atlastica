import { useCallback, useState } from "react";
import { AtlasMap } from "./AtlasMap";

interface SelectedCountry {
  isoCode: string;
  name: string;
}

function App() {
  const [selected, setSelected] = useState<SelectedCountry | null>(null);

  const handleSelectCountry = useCallback((isoCode: string, name: string) => {
    setSelected({ isoCode, name });
  }, []);

  return (
    <div style={{ position: "fixed", inset: 0 }}>
      <AtlasMap onSelectCountry={handleSelectCountry} />
      {selected && (
        <aside
          style={{
            position: "absolute",
            top: 12,
            right: 12,
            width: 280,
            background: "rgba(20, 20, 20, 0.9)",
            color: "#fff",
            padding: 16,
            borderRadius: 8,
            fontFamily: "system-ui, sans-serif",
          }}
        >
          <h2 style={{ margin: "0 0 4px", fontSize: 18 }}>{selected.name}</h2>
          <p style={{ margin: 0, opacity: 0.7, fontSize: 13 }}>{selected.isoCode}</p>
        </aside>
      )}
    </div>
  );
}

export default App;
