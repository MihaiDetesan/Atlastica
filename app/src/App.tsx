import { useCallback, useState } from "react";
import { AtlasMap } from "./AtlasMap";
import { CountryPanel } from "./CountryPanel";

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
        <CountryPanel key={selected.isoCode} isoCode={selected.isoCode} name={selected.name} onClose={() => setSelected(null)} />
      )}
    </div>
  );
}

export default App;
