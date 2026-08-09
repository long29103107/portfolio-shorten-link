type AppTopbarProps = {
  title: string;
  description: string;
};

export function AppTopbar({ title, description }: AppTopbarProps) {
  return (
    <header className="topbar">
      <div>
        <p className="eyebrow">Shorten Link</p>
        <h1 className="app-title">{title}</h1>
        <p className="page-description">{description}</p>
      </div>
    </header>
  );
}
