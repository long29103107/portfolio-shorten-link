import {
  createExpiryPresetValue,
  EXPIRY_PRESETS
} from "../expiryPresentation";

type ExpiryQuickPicksProps = {
  onChange: (expiredAtLocal: string) => void;
};

export function ExpiryQuickPicks({ onChange }: ExpiryQuickPicksProps) {
  return (
    <div className="expiry-quick-picks" aria-label="Quick expiry choices">
      {EXPIRY_PRESETS.map((option) => (
        <button
          key={option.label}
          type="button"
          className="expiry-quick-button"
          onClick={() => onChange(createExpiryPresetValue(new Date(), option.minutes))}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
