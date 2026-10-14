
type FeaturePageProps = {
  title: string
  description: string
}

export default function FeaturePage({
  title,
  description,
}: FeaturePageProps) {
  return (
    <section>
      <h2>{title}</h2>
      <p>{description}</p>
    </section>
  )
}
